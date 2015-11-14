using System;
using System.Collections.Generic;
using SteamKit2;
using log4net;
using System.Threading;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;

namespace Twitch2Steam
{
    public delegate void FriendMessageEventHandler (SteamFriends.FriendMsgCallback callback);
    public delegate void OfflineMessageEventHandler(SteamID user, ReadOnlyCollection<SteamFriends.FriendMsgHistoryCallback.FriendMessage> messages);
    
    //public delegate void FriendAcceptedEventHandler(SteamFriends.FriendsListCallback.Friend friend);

    public class SteamBot : IDisposable
    {
        private readonly ExponentialBackoff reconnectBackoff;
        private readonly ILog log = LogManager.GetLogger(typeof(SteamBot));

        private readonly SteamClient steamClient;
        private readonly CallbackManager manager;

        private readonly SteamUser steamUser;
        private readonly SteamFriends steamFriends;
        private readonly SteamApps steamApps;

        //Steamguard Helpers
        private String authCode;
        private String twoFactorCode;

        private volatile bool isRunning;

        public event FriendMessageEventHandler OnFriendMessage;
        //public event FriendAcceptedEventHandler OnFriendAccepted;
        public event OfflineMessageEventHandler OnOfflineMessage;

        public SteamBot()
        {
            reconnectBackoff = new ExponentialBackoff();

            // create our steamclient instance
            steamClient = new SteamClient();
            // create the callback manager which will route callbacks to function calls
            manager = new CallbackManager(steamClient);

            // get the steamuser handler, which is used for logging on after successfully connecting
            steamUser = steamClient.GetHandler<SteamUser>();
            // get the steam friends handler, which is used for interacting with friends on the network after logging on
            steamFriends = steamClient.GetHandler<SteamFriends>();
            steamApps = steamClient.GetHandler<SteamApps>();

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
            manager.Subscribe<SteamFriends.FriendMsgHistoryCallback>(ch_OnOfflineMessage2);
            manager.Subscribe<SteamFriends.ChatInviteCallback>(OnChatInvite);
            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
        }

        public void Connect()
        {
            isRunning = true;
            log.Info("Connecting...");

            // initiate the connection
            steamClient.Connect();
        }

        private void OnChatInvite( SteamFriends.ChatInviteCallback callback )
        {
            if(callback.ChatRoomType == EChatRoomType.Lobby)
            {
                log.Debug($"Game invite into game {callback.GameID} by {SteamIdToName(callback.InvitedID)}");
                SendChatMessage(callback.InvitedID, "I don't even have that game :steamsad:");
                //callback.GameID.ToString
            }
            else
            {
                log.Warn($"Unexcpected chatinvite of type {callback.ChatRoomType} into {callback.ChatRoomName} by {SteamIdToName(callback.PatronID)} ");
            }
        }

        private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            if (OnFriendMessage != null)
                OnFriendMessage(callback);
        }

        private void ch_OnOfflineMessage2(SteamFriends.FriendMsgHistoryCallback messages)
        {
            if (OnOfflineMessage != null)
                OnOfflineMessage.Invoke(messages.SteamID, messages.Messages);
        }

        private void EchoMsg(SteamFriends.FriendMsgEchoCallback callback)
        {
            log.Warn($"Echo to {SteamIdToName(callback.Recipient)}: {callback.Message}");
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

            log.Info($"Connected to Steam! Logging in as {Settings.Default.SteamName}'");

            byte[] sentryHash = null;
            if (File.Exists("sentry.bin"))
            {
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = Settings.Default.SteamName,
                Password = Settings.Default.SteamPassword,
                AuthCode = authCode,
                TwoFactorCode = twoFactorCode,
                SentryFileHash = sentryHash
            });

            reconnectBackoff.Reset();
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            if (callback.UserInitiated)
            {
                //isRunning = false;
                log.Info("Disconnected from Steam");
            }
            else
            {
                TimeSpan timeout = reconnectBackoff.NextDelay;
                log.Error($"Lost connection from Steam, trying to reconnect in {timeout.ToReadableString()}.");
                Thread.Sleep(timeout);
                steamClient.Connect();
            }
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            //TODO Warn users if twitch disconnected, but ensure that steam is connected first. 
            //this.Broadcast("Warning: I went offline");


            if (callback.Result == EResult.AccountLoginDeniedNeedTwoFactor)
            {
                Console.Error.WriteLine("Account is protected by SteamGuard. Please enter code from your mobile app: ");
                twoFactorCode = Console.ReadLine();
                return;
            }
            else if (callback.Result == EResult.AccountLogonDenied)
            {
                Console.Error.WriteLine($"Account is protected by SteamGuard. Please enter the code sent to your account at {callback.EmailDomain}: ");
                authCode = Console.ReadLine();
                return;
            }

            if (callback.Result != EResult.OK)
            {
                log.Fatal($"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");

                isRunning = false;
                return;
            }


            //TODO: Don't hardcode
            log.Debug("Flags: " + callback.AccountFlags);

            var possibleFlags = Enum.GetValues(typeof(EAccountFlags)).Cast<Enum>();
            var expectedFlags = (EAccountFlags.PersonaNameSet | EAccountFlags.PasswordSet | EAccountFlags.HWIDSet | EAccountFlags.LimitedUser | EAccountFlags.Steam2MigrationComplete | EAccountFlags.EmailValidated | EAccountFlags.LogonExtraSecurity);

            foreach (var flag in possibleFlags.Where(flag => callback.AccountFlags.HasFlag(flag) && !expectedFlags.HasFlag(flag)))
            {
                log.Warn("Unexpected Account Flag: " + flag);
            }

            foreach (var flag in possibleFlags.Where(flag => !callback.AccountFlags.HasFlag(flag) && expectedFlags.HasFlag(flag)))
            {
                log.Warn("Expected Account Flag missing: " + flag);
            }

            log.Info("Successfully logged on!");

            steamFriends.RequestOfflineMessages();
        }

        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            log.Info("Going online...");
            // at this point, we can go online on friends, so lets do that
            steamFriends.SetPersonaState(EPersonaState.Online);

            /*
            var playGame = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
            playGame.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = new GameID(550),
            });

            // notice here we're sending this message directly using the SteamClient
            steamClient.Send(playGame);
            */
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
            foreach (SteamFriends.FriendsListCallback.Friend friend in callback.FriendList)
            {
                if (friend.Relationship == EFriendRelationship.RequestRecipient)
                {
                    // this user has added us, let's add him back
                    steamFriends.AddFriend(friend.SteamID);
                }
            }
        }

        private void OnFriendAdded(SteamFriends.FriendAddedCallback callback)
        {
            // someone accepted our friend request, or we accepted one
            log.Debug($"{SteamIdToName(callback.SteamID)} is now a friend");
            SendChatMessage(callback.SteamID, $"HI THERE {callback.PersonaName.ToUpper()}!");
            SendChatMessage(callback.SteamID, "Say 'help' for a list of commands");
        }

        private void OnPersonaState(SteamFriends.PersonaStateCallback callback)
        {
            // this callback is received when the persona state (friend information) of a friend changes
            if(String.IsNullOrEmpty(callback.GameName))
                log.Debug($"State change: {callback.Name} is now {callback.State}");
            else
                log.Debug($"State change: {callback.Name} is now playing {callback.GameName}");
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            log.Warn("Logged off of Steam: " + callback.Result);
        }

        private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            log.Debug("Received new sentry file");
            int fileSize;
            byte[] sentryHash;
            using (var fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = ( int )fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = new SHA1CryptoServiceProvider())
                {
                    sentryHash = sha.ComputeHash(fs);
                }
            }

            // inform the steam servers that we're accepting this sentry file
            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,

                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = fileSize,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,

                OneTimePassword = callback.OneTimePassword,

                SentryFileHash = sentryHash,
            });
        }


        public void Exit()
        {
            isRunning = false;
            steamClient.Disconnect();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(Boolean disposing)
        {
            if (disposing)
            {
                Exit();
            }
        }

        public String GetGamePlayedByFriend( SteamID friend )
        {
            return steamFriends.GetFriendGamePlayedName(friend);
        }

        public String SteamIdToName(SteamID id)
        {
            return steamFriends.GetFriendPersonaName(id) + " [" + id.Render() + "]";
        }
    }
}