using Sharkbite.Irc;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Specialized;
using SteamKit2.Internal;

namespace Twitch2Steam
{
    public class Glue
    {
        private readonly Object myLock; //TODO more fine grained lock?
        private readonly TwitchBot twitchBot;
        private readonly SteamBot steamBot;
        private readonly Dictionary<String, ISet<SteamID>> subscriptionsUsersMap;
        private readonly Dictionary<SteamID, ISet<String>> usersSubscriptionsMap;

        private readonly ISet<SteamID> adminList;

        public Glue(TwitchBot twitchBot, SteamBot steamBot)
        {
            myLock = new Object();
            this.twitchBot = twitchBot;
            this.steamBot = steamBot;
            subscriptionsUsersMap = new Dictionary<String, ISet<SteamID>>();
            usersSubscriptionsMap = new Dictionary<SteamID, ISet<String>>();

            adminList = LoadAdmins();
            
            twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { Console.WriteLine(user.Nick + ": " + message); };
            //twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { steamBot.Broadcast(user.Nick + ": " + message); };
            twitchBot.OnPublicMessage += OnTwitchPublicMessage;
            steamBot.OnFriendMessage += OnSteamFriendMessage;
            steamBot.OnOfflineMessage += steamBot_OnOfflineMessage;
        }

        private ISet<SteamID> LoadAdmins()
        {
            var admins = new HashSet<SteamID>();
            foreach(var admin in Settings.Default.Admins)
            {
                var adminId = new SteamID(admin);
                admins.Add(adminId);
                Console.WriteLine("\t" + steamBot.SteamIdToName(adminId) + " is an admin");
            }
            Console.WriteLine("{0} admin(s) loaded", admins.Count);
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
                ISet<SteamID> users;
                subscriptionsUsersMap.TryGetValue(channel, out users);
                if (users == null)
                    return;

                foreach (var target in users)
                {
                    steamBot.SendChatMessage(target, user.Nick + ": " + message);
                }
            }
        }
        
        private void steamBot_OnOfflineMessage(SteamID user, List<CMsgClientFSGetFriendMessageHistoryResponse.FriendMessage> messages)
        {
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

                default:
                    Console.WriteLine("->Unusual chat message of type {0},  from {1} with content '{2}'.", callback.EntryType, steamBot.SteamIdToName(callback.Sender), callback.Message);
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
                        System.Threading.Thread.Sleep(1000); //TODO FIX UGLY AF
                        Exit();
                    }
                    else
                    {
                        steamBot.SendChatMessage(sender, "You are not my master! >:( I will tell on you.");
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
                        steamBot.SendChatMessage(sender, "You don't get to tell me stuff!");
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
                                adminList.Add(newAdminId);
                                writeAdmins();
                                Console.WriteLine("***ADMIN ADDED***");
                                steamBot.SendChatMessage(sender, steamBot.SteamIdToName(newAdminId) + " is now admin.");
                                steamBot.SendChatMessage(newAdminId, "You're my master now! Weeee! :steamhappy:");
                            }
                        }
                        else
                        {
                            Console.WriteLine("\tSteamID is invalid");
                            steamBot.SendChatMessage(sender, "Invalid SteamID");
                        }
                    }
                    else
                    {
                        Console.WriteLine("***DENIED***");
                        steamBot.SendChatMessage(sender, "Nice Try! :steamfacepalm:");
                    }
                }
                #endregion
                #region list
                else if (lowerMessage.Equals("list"))
                {
                    String channels = "";
                    foreach (var key in subscriptionsUsersMap.Keys)
                    {
                        if (subscriptionsUsersMap[key].Contains(sender))
                            channels += key + " ";
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
                    int removeCount = 0;
                    var toPart = new HashSet<String>();
                    foreach (var key in subscriptionsUsersMap.Keys)
                    {
                        var subs = subscriptionsUsersMap[key];
                        if (subs.Remove(sender))
                            removeCount++;
                        if (subs.Count == 0)
                            toPart.Add(key);
                    }

                    foreach (var key in toPart)
                    {
                        subscriptionsUsersMap.Remove(key);
                        twitchBot.Leave(key);
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

                    ISet<SteamID> channelSubs;
                    subscriptionsUsersMap.TryGetValue(channel, out channelSubs);
                    if (channelSubs == null)
                    {
                        channelSubs = new HashSet<SteamID>();
                        subscriptionsUsersMap.Add(channel, channelSubs);
                        twitchBot.Join(channel);
                    }

                    if (channelSubs.Add(sender))
                    {
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

                    ISet<SteamID> channelSubs;
                    subscriptionsUsersMap.TryGetValue(channel, out channelSubs);
                    
                    if (channelSubs != null && channelSubs.Remove(sender))
                    {
                        steamBot.SendChatMessage(sender, "OK, you are not subscribed to " + channel + " anymore");
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
                else if(lowerMessage.StartsWith("say "))
                {                    
                    if (adminList.Contains(sender))
                    {
                        String[] data = message.Split(new char[] { ' ' }, 3);
                        String channel = data[1].ToLower();

                        if (!channel.StartsWith("#"))
                            channel = "#" + channel;

                        if (data.Length == 3)
                        {

                            if (subscriptionsUsersMap.Keys.Contains(channel) && subscriptionsUsersMap[channel].Contains(sender))
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
                                steamBot.SendChatMessage(sender, "That's not a valid SteamID :/");
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

                //Console.WriteLine(Sender.Render() + ": " + callback.Message);
                Console.WriteLine(steamBot.SteamIdToName(sender) + ": " + message);

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
