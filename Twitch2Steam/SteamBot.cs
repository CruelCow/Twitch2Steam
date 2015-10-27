using System;
using System.Collections.Generic;
using SteamKit2;
using SteamKit2.Internal;
using log4net;

namespace Twitch2Steam
{
    public delegate void FriendMessageEventHandler (SteamFriends.FriendMsgCallback callback);
    public delegate void OfflineMessageEventHandler(SteamID user, List<CMsgClientFSGetFriendMessageHistoryResponse.FriendMessage> messages);
    
    //public delegate void FriendAcceptedEventHandler(SteamFriends.FriendsListCallback.Friend friend);

    public class SteamBot : IDisposable
    {
        private readonly ILog log = LogManager.GetLogger(typeof(SteamBot));

        private readonly SteamClient steamClient;
        private readonly CallbackManager manager;

        private readonly SteamUser steamUser;
        private readonly SteamFriends steamFriends;

        private volatile bool isRunning;

        public event FriendMessageEventHandler OnFriendMessage;
        //public event FriendAcceptedEventHandler OnFriendAccepted;
        public event OfflineMessageEventHandler OnOfflineMessage;

        public SteamBot()
        {
            // create our steamclient instance
            steamClient = new SteamClient();
            // create the callback manager which will route callbacks to function calls
            manager = new CallbackManager(steamClient);

            // get the steamuser handler, which is used for logging on after successfully connecting
            steamUser = steamClient.GetHandler<SteamUser>();
            // get the steam friends handler, which is used for interacting with friends on the network after logging on
            steamFriends = steamClient.GetHandler<SteamFriends>();

            steamClient.AddHandler(new CustomHandler());

            // register a few callbacks we're interested in
            // these are registered upon creation to a callback manager, which will then route the callbacks
            // to the functions specified
            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            //manager.Subscribe<SteamUser.MarketingMessageCallback>(OnMarketing);

            // we use the following callbacks for friends related activities
            manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);
            manager.Subscribe<SteamFriends.PersonaStateCallback>(OnPersonaState);
            manager.Subscribe<SteamFriends.FriendAddedCallback>(OnFriendAdded);
            manager.Subscribe<SteamFriends.FriendMsgCallback>(OnFriendMsg);
            manager.Subscribe<SteamFriends.FriendMsgEchoCallback>(EchoMsg);
            manager.Subscribe<SteamFriends.CMsgClientFSGetFriendMessageHistoryResponseCallback>(ch_OnOfflineMessage2);
            

            isRunning = true;

            log.Info("Connecting...");

            // initiate the connection
            steamClient.Connect();
        }
        
        private void ch_OnOfflineMessage2(SteamFriends.CMsgClientFSGetFriendMessageHistoryResponseCallback messages)
        {
            if (OnOfflineMessage != null)
                OnOfflineMessage.Invoke(messages.SteamID, messages.Messages);
        }

        private void ch_OnOfflineMessage(CMsgClientFSGetFriendMessageHistoryResponse messages)
        {
            if (OnOfflineMessage != null)
                OnOfflineMessage.Invoke(messages.steamid, messages.messages);
        }

        private void EchoMsg(SteamFriends.FriendMsgEchoCallback obj)
        {
            log.Info("******ECHO: " + obj);
        }

        public void loop()
        {
            while (isRunning)
            {
                // in order for the callbacks to get routed, they need to be handled by the manager
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(10));
            }
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                log.Fatal("Unable to connect to Steam: " + callback.Result);

                isRunning = false; //TODO remove?
                return;
            }

            log.Info("Connected to Steam! Logging in as {Settings.Default.SteamName}'");

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = Settings.Default.SteamName,
                Password = Settings.Default.SteamPassword,
            });
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            if (callback.UserInitiated)
            {
                log.Info("Disconnected from Steam");
            }
            else
            {
                log.Warn("Lost connection from Steam, trying to reconnect...");

                //isRunning = false;
                steamClient.Connect();
            }
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            log.Info("Flags: " + callback.AccountFlags);;
                        
            if (callback.Result != EResult.OK)
            {
                if (callback.Result == EResult.AccountLogonDenied)
                {
                    // if we recieve AccountLogonDenied or one of it's flavors (AccountLogonDeniedNoMailSent, etc)
                    // then the account we're logging into is SteamGuard protected
                    // see sample 5 for how SteamGuard can be handled

                    log.Fatal("Unable to logon to Steam: This account is SteamGuard protected.");
                }
                else
                {
                    log.Fatal($"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");
                }

                isRunning = false;
                return;
            }

            log.Info("Successfully logged on!");

            //Request offline messages. CustomHandler actually handles the messages
            //var x = new ClientMsgProtobuf<CMsgClientFSGetFriendMessageHistoryForOfflineMessages>(EMsg.ClientFSGetFriendMessageHistoryForOfflineMessages);
            //steamClient.Send(x);
            steamFriends.RequestOfflineMessages();
        }

        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            log.Info("Going online...");
            // at this point, we can go online on friends, so lets do that
            steamFriends.SetPersonaState(EPersonaState.Online);
        }

        private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            if (OnFriendMessage != null)
                OnFriendMessage(callback);
        }

        public void SendChatMessage(SteamID target, String message)
        {
            steamFriends.SendChatMessage(target, EChatEntryType.ChatMsg, message);
        }

        public bool IsFriend(SteamID target)
        {
            return steamFriends.GetFriendRelationship(target) == EFriendRelationship.Friend;
        }

        public void Broadcast(String message)
        {
            int friendCount = steamFriends.GetFriendCount();

            for (int x = 0; x < friendCount; x++)
            {
                SteamID steamIdFriend = steamFriends.GetFriendByIndex(x);
                steamFriends.SendChatMessage(steamIdFriend, EChatEntryType.ChatMsg, message);
            }
        }

        private void OnFriendsList(SteamFriends.FriendsListCallback callback)
        {
            // at this point, the client has received it's friends list

            foreach (SteamFriends.FriendsListCallback.Friend friend in callback.FriendList)
            {
                if (friend.Relationship == EFriendRelationship.RequestRecipient)
                {
                    // this user has added us, let's add him back
                    steamFriends.AddFriend(friend.SteamID);
                    SendChatMessage(friend.SteamID, "HI THERE!");
                    SendChatMessage(friend.SteamID, "Say 'help' for a list of commands");
                }
            }
        }

        private void OnFriendAdded(SteamFriends.FriendAddedCallback callback)
        {
            // someone accepted our friend request, or we accepted one
            log.Debug($"{SteamIdToName(callback.SteamID)} is now a friend");
        }

        private void OnPersonaState(SteamFriends.PersonaStateCallback callback)
        {
            // this callback is received when the persona state (friend information) of a friend changes
            log.Debug($"State change: {callback.Name} is now {callback.State}");
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            log.Warn("Logged off of Steam: " + callback.Result);
        }

        public void Exit()
        {
            isRunning = false;
            steamClient.Disconnect();
        }

        public virtual void Dispose()
        {
            Exit();
        }

        public String SteamIdToName(SteamID id)
        {
            return steamFriends.GetFriendPersonaName(id) + " [" + id.Render() + "]";
        }
    }
}