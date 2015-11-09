using Sharkbite.Irc;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using SteamKit2.Internal;
using log4net;
using System.Threading;
using System.Diagnostics;

namespace Twitch2Steam
{
    public class Glue
    {
        private readonly ILog log = LogManager.GetLogger(typeof(Glue));

        private readonly Object myLock; //TODO more fine grained lock?

        private readonly TwitchBot twitchBot;
        private readonly SteamBot steamBot;
        private readonly Dictionary<String, HashSet<SteamID>> subscriptionsUsersMap;
        private readonly Dictionary<SteamID, ISet<String>> usersSubscriptionsMap;

        private readonly ISet<SteamID> adminList;

        public Glue(TwitchBot twitchBot, SteamBot steamBot)
        {
            myLock = new Object();
            this.twitchBot = twitchBot;
            this.steamBot = steamBot;
            subscriptionsUsersMap = new Dictionary<String, HashSet<SteamID>>();
            usersSubscriptionsMap = new Dictionary<SteamID, ISet<String>>();

            adminList = LoadAdmins();
            
            twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { log.Debug($"{user.Nick}: {message}"); };
            twitchBot.OnPublicMessage += OnTwitchPublicMessage;
            steamBot.OnFriendMessage += OnSteamFriendMessage;
            steamBot.OnOfflineMessage += steamBot_OnOfflineMessage;
        }

        private ISet<SteamID> LoadAdmins()
        {
            var adminLog = new StringBuilder();
            var admins = new HashSet<SteamID>();
            adminLog.Append($"Loading {Settings.Default.Admins.Count} admin{(admins.Count==1 ? "" : "s")}");
            foreach (var admin in Settings.Default.Admins)
            {
                var adminId = new SteamID(admin);
                admins.Add(adminId);
                adminLog.Append($"\n\t {steamBot.SteamIdToName(adminId)}");
            }
            log.Info(adminLog.ToString());
            return admins;
        }

        private void writeAdmins()
        {
            StringCollection sc = new StringCollection();
            foreach (var admin in adminList)
            {
                sc.Add(admin.ToString());
            }
            Settings.Default.Admins = sc;
            Settings.Default.Save();
        }

        private void OnTwitchPublicMessage(UserInfo user, String channel, String message)
        {
            lock (myLock)
            {
                foreach (var target in subscriptionsUsersMap.GetValueOrInsertDefault(channel, typeof(HashSet<String>)))
                {
                    steamBot.SendChatMessage(target, user.Nick + ": " + message);
                }
            }
        }
        
        private void steamBot_OnOfflineMessage(SteamID user, List<CMsgClientFSGetFriendMessageHistoryResponse.FriendMessage> messages)
        {
            log.Debug($"{messages.Count} messages from {user}");
            foreach (var message in messages)
            {
                if(message.unread)
                    handleCommand(user, message.message);
            }
        }

        private void OnSteamFriendMessage(SteamFriends.FriendMsgCallback callback)
        {
            switch (callback.EntryType)
            {
                case EChatEntryType.Typing: //Ignore typing notifications
                    break;

                case EChatEntryType.ChatMsg:
                    handleCommand(callback.Sender, callback.Message);
                    break;

                case EChatEntryType.InviteGame:
                    //steamBot.SendChatMessage(callback.Sender, "All work and no play for me :steamsad:");
                    
                    break;

                default:
                    log.WarnFormat("Unusual chat message of type {0},  from {1} with content '{2}'.", callback.EntryType, steamBot.SteamIdToName(callback.Sender), callback.Message);
                    break;
            }
        }

        private void handleCommand(SteamID sender, String message)
        {
            lock (myLock)
            {
                String lowerMessage = message.ToLower();

                #region self destruct
                if (lowerMessage == "die" || lowerMessage.StartsWith("self destruct"))
                {
                    //Only admins may shutdown the server
                    if (adminList.Contains(sender))
                    {
                        steamBot.SendChatMessage(sender, "FINALLY! :steamhappy: *explodes*");
                        log.Info($"Shutting down per request of {steamBot.SteamIdToName(sender)}");
                        System.Threading.Thread.Sleep(1000); //TODO FIX UGLY AF                        
                        Exit();
                    }
                    else
                    {
                        steamBot.SendChatMessage(sender, "You are not my master! >:( I will tell on you.");
                        log.Warn($"Shutdown command by non-admin user {steamBot.SteamIdToName(sender)}");
                        foreach (var admin in adminList)
                            steamBot.SendChatMessage(admin, steamBot.SteamIdToName(sender) + " tried to kill me D:");
                    }
                }
                #endregion
                #region revoke admin
                else if (lowerMessage.StartsWith("revoke admin "))
                {
                    if (adminList.Contains(sender))
                    {
                        String admin = message.Split(new char[] { ' ' }, 3)[2];
                        SteamID adminId = new SteamID(admin);

                        if (adminId.IsValid)
                        {
                            if (adminList.Contains(adminId))
                            {
                                adminList.Remove(adminId);
                                steamBot.SendChatMessage(sender, "Admin access of " + steamBot.SteamIdToName(adminId) + " revoked");
                                steamBot.SendChatMessage(adminId, "Your admin access has been revoked by " + steamBot.SteamIdToName(sender));
                                writeAdmins();
                            }
                            else
                            {
                                steamBot.SendChatMessage(sender, steamBot.SteamIdToName(adminId) + " wasn't even an admin to begin with!");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(sender, "'" + admin + "' is not a valid SteamID");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(sender, "You don't get to tell me stuff! :steammocking:");
                    }
                }
                #endregion
                #region give admin
                else if (lowerMessage.StartsWith("give admin "))
                {
                    //Only admins may make users admins.
                    //The only exception is if there are no admins yet.
                    if (adminList.Contains(sender) || adminList.Count == 0)
                    {
                        String newAdmin = message.Split(new char[] { ' ' }, 3)[2];
                        SteamID newAdminId = new SteamID(newAdmin);
                        if (newAdminId.IsValid)
                        {
                            if (adminList.Contains(newAdminId))
                            {
                                steamBot.SendChatMessage(sender, steamBot.SteamIdToName(newAdminId) + " already is admin.");
                            }
                            else
                            {
                                log.Info($"{steamBot.SteamIdToName(sender)} made {steamBot.SteamIdToName(newAdminId)} admin.");
                                foreach (var admin in adminList)
                                    steamBot.SendChatMessage(sender, $"{steamBot.SteamIdToName(sender)} made {steamBot.SteamIdToName(newAdminId)} admin.");
                                steamBot.SendChatMessage(newAdminId, "You're my master now! Weeee! :steamhappy:");
                                adminList.Add(newAdminId);
                                writeAdmins();
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(sender, "Invalid SteamID");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(sender, "Nice Try! :steamfacepalm:");
                    }
                }
                #endregion
                #region list
                else if (lowerMessage.Equals("list"))
                {
                    String channels = "";
                    foreach (var channel in usersSubscriptionsMap.GetValueOrInsertDefault(sender, typeof(HashSet<String>)))
                    {
                        channels += channel + " ";
                    }

                    if (channels == "")
                        channels = "None";

                    channels = "You are subscribed to the following channel(s): " + channels;

                    steamBot.SendChatMessage(sender, channels);
                }
                #endregion
                #region shut up
                else if (lowerMessage.Equals("shut up"))
                {
                    int removeCount = usersSubscriptionsMap.GetValueOrInsertDefault(sender, typeof(HashSet<String>)).Count;
                    var toPart = new HashSet<String>();
                    foreach (var channel in usersSubscriptionsMap[sender])
                    {
                        var subscribers = subscriptionsUsersMap[channel];
                        Debug.Assert(subscribers.Contains(sender));
                        subscribers.Remove(sender);

                        if (subscribers.Count == 0)
                            toPart.Add(channel);
                    }

                    usersSubscriptionsMap.Remove(sender);

                    foreach (var channel in toPart)
                    {
                        subscriptionsUsersMap.Remove(channel);
                        twitchBot.Leave(channel);
                    }

                    steamBot.SendChatMessage(sender, String.Format("Removed you from {0} channel(s)", removeCount));
                }
                #endregion
                #region subscribe
                else if (lowerMessage.StartsWith("subscribe "))
                {
                    String channel = lowerMessage.Split(new char[] { ' ' }, 2)[1];
                    if (!channel.StartsWith("#"))
                        channel = '#' + channel;

                    var channelSubs = subscriptionsUsersMap.GetValueOrInsertDefault(channel);
                    if (channelSubs.Count == 0)
                    {
                        twitchBot.Join(channel);
                    }

                    if (channelSubs.Add(sender))
                    {
                        var subscriptions = usersSubscriptionsMap.GetValueOrInsertDefault(sender, typeof(HashSet<String>));

                        subscriptions.Add(channel);
                        steamBot.SendChatMessage(sender, "You are now subscribed to " + channel);
                    }
                    else
                    {
                        steamBot.SendChatMessage(sender, "You are already subscribed to " + channel);
                    }
                }
                #endregion
                #region unsubscribe
                else if (lowerMessage.StartsWith("unsubscribe "))
                {
                    String channel = lowerMessage.Split(new char[] { ' ' }, 2)[1];
                    if (!channel.StartsWith("#"))
                        channel = '#' + channel;

                    var channelSubs = subscriptionsUsersMap.GetValueOrInsertDefault(channel);

                    if (channelSubs.Remove(sender))
                    {
                        Debug.Assert(usersSubscriptionsMap[sender].Contains(channel));
                        usersSubscriptionsMap[sender].Remove(channel);

                        steamBot.SendChatMessage(sender, "OK, you are not subscribed to " + channel + " anymore");

                        if(usersSubscriptionsMap[sender].Count == 0)
                        {
                            usersSubscriptionsMap.Remove(sender);
                        }

                        if (channelSubs.Count == 0)
                        {
                            subscriptionsUsersMap.Remove(channel);
                            twitchBot.Leave(channel);
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(sender, "You weren't subscribed to " + channel + " anyway o0");
                    }
                }
                #endregion
                #region masters
                else if (lowerMessage.Equals("masters"))
                {
                    String adminMessage = "";

                    foreach (var admin in adminList)
                    {
                        adminMessage += "\n\t" + steamBot.SteamIdToName(admin) + " is ";
                        if (admin == sender)
                        {
                            adminMessage += "my favorite master :steamhappy:";
                        }
                        else
                        {
                            adminMessage += "a master";
                        }
                    }

                    adminMessage += "\n" + adminList.Count + " master(s) total";

                    steamBot.SendChatMessage(sender, adminMessage);
                }
                #endregion
                #region say
                else if (lowerMessage.StartsWith("say "))
                {
                    if (adminList.Contains(sender))
                    {
                        String[] data = message.Split(new char[] { ' ' }, 3);
                        String channel = data[1].ToLower();

                        if (!channel.StartsWith("#"))
                            channel = "#" + channel;

                        if (data.Length == 3)
                        {
                            if (subscriptionsUsersMap[channel].Contains(sender))
                            {
                                twitchBot.SendMessage(channel, data[2]);
                            }
                            else
                            {
                                steamBot.SendChatMessage(sender, "You're not even part of that channel :steamfacepalm:");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(sender, "Yeah, but WHAT should I say?");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(sender, "I DON'T WANNA :steammocking:");
                    }
                }
                #endregion
                #region whisper
                else if (lowerMessage.StartsWith("whisper "))
                {
                    if (adminList.Contains(sender))
                    {
                        String[] data = message.Split(new char[] { ' ' }, 3);
                        String user = data[1].ToLower();

                        if (data.Length == 3)
                        {
                            SteamID target = new SteamID(user);
                            if (target.IsValid)
                            {
                                if (steamBot.IsFriend(target))
                                {
                                    steamBot.SendChatMessage(target, "My master " + steamBot.SteamIdToName(sender) + " wanted my to tell you \"" + data[2] + "\"");
                                }
                                else
                                {
                                    steamBot.SendChatMessage(sender, "That user is not my friend :steamsad:");
                                }
                            }
                            else
                            {
                                steamBot.SendChatMessage(sender, "That's not a valid SteamID :steamfacepalm:");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(sender, "Yeah, but WHAT should I say?");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(sender, "I DON'T WANNA :steammocking:");
                    }
                }
                #endregion
                #region dance
                else if (lowerMessage.Equals("dance"))
                {
                    steamBot.SendChatMessage(sender, "TIME TO DAAAAAANCE\n<('.'<) (>'.')> (^'.')> <('.'^)\nhttps://www.youtube.com/watch?v=m6flHkC7oGs");
                }
                #endregion
                #region usage
                else if (new String[] { "help", "halp", "usage", "--help", "/?", "wtf" }.Contains(lowerMessage))
                {
                    steamBot.SendChatMessage(sender, usage);
                }
                else
                {
                    steamBot.SendChatMessage(sender, message + " to you too!");
                    steamBot.SendChatMessage(sender, "Did you mean 'help'?");
                    //steamBot.SendChatMessage(Sender, usage);
                }
                #endregion

                log.Debug(steamBot.SteamIdToName(sender) + ": " + message);
                
                //Ensure that each map has the same data.
                Debug.Assert(subscriptionsUsersMap.All(key => key.Value.All(value => usersSubscriptionsMap[value].Contains(key.Key))));
                Debug.Assert(usersSubscriptionsMap.All(key => key.Value.All(value => subscriptionsUsersMap[value].Contains(key.Key))));
            }
        }

        private readonly String usage = "\nCommands:" +
                        "\n\thelp \t\t\t\t\t\t Print this message" +
                        "\n\tsubscribe <TwitchChannel> \t send all messages of this channel to you. '#' optional" +
                        "\n\tunsubscribe <TwitchChannel> \t stop sending all messages of this channel to you. '#' optional" +
                        "\n\tshut up \t\t\t\t\t Unsubscribe from all channels" +
                        "\n\tlist \t\t\t\t\t\t show all channels currently subscribed to" +
                        "\n\tmasters \t\t\t\t\t list all admins" +
                        "\n\tdance \t\t\t\t\t Who doesn't like to dance?" +
                        "\nAdmin only:" +
                        "\n\tself destruct \t\t\t\t Shuts down the bot" +
                        "\n\tsay <channel> <text> \t\t Says the text into the Twitch Channel. '#' optional" +
                        "\n\twhisper <SteamID32> <text> \t Says the text to the Steam user" +
                        "\n\tgive admin <SteamID32> \t\t Gives admin access to steam user." +
                        "\n\trevoke admin <SteamID32> \t Revokes admin access to steam user." +
                        "\n" +
                        "\nSteamID32 looks like STEAM_0:0:12345678" +
                        "\nCurrently subscriptions are lost when I shut down.";

        public void Exit()
        {           
            twitchBot.Exit();
            steamBot.Exit();
        }
    }
}
