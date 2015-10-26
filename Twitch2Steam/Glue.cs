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
        private readonly Dictionary<String, IList<SteamID>> subscribers;

        private readonly IList<SteamID> adminList;

        public Glue(TwitchBot twitchBot, SteamBot steamBot)
        {
            myLock = new Object();
            this.twitchBot = twitchBot;
            this.steamBot = steamBot;
            subscribers = new Dictionary<String, IList<SteamID>>();
            adminList = LoadAdmins();
            
            twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { Console.WriteLine(user.Nick + ": " + message); };
            //twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { steamBot.Broadcast(user.Nick + ": " + message); };
            twitchBot.OnPublicMessage += OnTwitchPublicMessage;
            steamBot.OnFriendMessage += OnSteamFriendMessage;
            steamBot.OnOfflineMessage += steamBot_OnOfflineMessage;
        }

        private IList<SteamID> LoadAdmins()
        {
            var admins = new List<SteamID>();
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
                IList<SteamID> users;
                subscribers.TryGetValue(channel, out users);
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

        private void handleCommand(SteamID Sender, String Message)
        {
            lock (myLock)
            {
                String lowerMessage = Message.ToLower();

                #region self destruct
                if (lowerMessage == "die" || lowerMessage.StartsWith("self destruct"))
                {
                    //Only admins may shutdown the server
                    if (adminList.Contains(Sender))
                    {
                        steamBot.SendChatMessage(Sender, "FINALLY! :steamhappy: *explodes*");
                        System.Threading.Thread.Sleep(1000); //TODO FIX UGLY AF
                        Exit();
                    }
                    else
                    {
                        steamBot.SendChatMessage(Sender, "You are not my master! >:( I will tell on you.");
                        foreach (var admin in adminList)
                            steamBot.SendChatMessage(admin, steamBot.SteamIdToName(Sender) + " tried to kill me D:");
                    }
                }
                #endregion
                #region revoke admin
                else if (lowerMessage.StartsWith("revoke admin "))
                {
                    if (adminList.Contains(Sender))
                    {
                        String admin = Message.Split(new char[] { ' ' }, 3)[2];
                        SteamID adminId = new SteamID(admin);

                        if (adminId.IsValid)
                        {
                            if (adminList.Contains(adminId))
                            {
                                adminList.Remove(adminId);
                                steamBot.SendChatMessage(Sender, "Admin access of " + steamBot.SteamIdToName(adminId) + " revoked");
                                steamBot.SendChatMessage(adminId, "Your admin access has been revoked by " + steamBot.SteamIdToName(Sender));
                                writeAdmins();
                            }
                            else
                            {
                                steamBot.SendChatMessage(Sender, steamBot.SteamIdToName(adminId) + " wasn't even an admin to begin with!");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(Sender, "'" + admin + "' is not a valid SteamID");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(Sender, "You don't get to tell me stuff!");
                    }
                }
                #endregion
                #region give admin
                else if (lowerMessage.StartsWith("give admin "))
                {
                    //Only admins may make users admins.
                    //The only exception is when there are no admins yet.
                    if (adminList.Contains(Sender) || adminList.Count == 0)
                    {
                        String newAdmin = Message.Split(new char[] { ' ' }, 3)[2];
                        SteamID newAdminId = new SteamID(newAdmin);
                        if (newAdminId.IsValid)
                        {
                            if (adminList.Contains(newAdminId))
                            {
                                steamBot.SendChatMessage(Sender, steamBot.SteamIdToName(newAdminId) + " already is admin.");
                            }
                            else
                            {
                                adminList.Add(newAdminId);
                                writeAdmins();
                                Console.WriteLine("***ADMIN ADDED***");
                                steamBot.SendChatMessage(Sender, steamBot.SteamIdToName(newAdminId) + " is now admin.");
                                steamBot.SendChatMessage(newAdminId, "You're my master now! Weeee! :steamhappy:");
                            }
                        }
                        else
                        {
                            Console.WriteLine("\tSteamID is invalid");
                            steamBot.SendChatMessage(Sender, "Invalid SteamID");
                        }
                    }
                    else
                    {
                        Console.WriteLine("***DENIED***");
                        steamBot.SendChatMessage(Sender, "Nice Try! :steamfacepalm:");
                    }
                }
                #endregion
                #region list
                else if (lowerMessage.Equals("list"))
                {
                    String channels = "";
                    foreach (var key in subscribers.Keys)
                    {
                        if (subscribers[key].Contains(Sender))
                            channels += key + " ";
                    }

                    if (channels == "")
                        channels = "None";

                    channels = "You are subscribed to the following channel(s): " + channels;

                    steamBot.SendChatMessage(Sender, channels);
                }
                #endregion
                #region shut up
                else if (lowerMessage.Equals("shut up"))
                {
                    var toRemove = new List<String>();
                    foreach (var key in subscribers.Keys)
                    {
                        var subs = subscribers[key];
                        subs.Remove(Sender);
                        if (subs.Count == 0)
                            toRemove.Add(key);
                    }

                    foreach (var key in toRemove)
                    {
                        subscribers.Remove(key);
                        twitchBot.Leave(key);
                    }

                    steamBot.SendChatMessage(Sender, String.Format("Removed you from {0} channel(s)", toRemove.Count));
                }
                #endregion
                #region subscribe
                else if (lowerMessage.StartsWith("subscribe "))
                {
                    String channel = lowerMessage.Split(new char[] { ' ' }, 2)[1];
                    if (!channel.StartsWith("#"))
                        channel = '#' + channel;

                    IList<SteamID> channelSubs;
                    subscribers.TryGetValue(channel, out channelSubs);
                    if (channelSubs == null)
                    {
                        channelSubs = new List<SteamID>();
                        subscribers.Add(channel, channelSubs);
                        twitchBot.Join(channel);
                    }

                    channelSubs.Add(Sender);
                }
                #endregion
                #region unsubscribe
                else if (lowerMessage.StartsWith("unsubscribe "))
                {
                    String channel = lowerMessage.Split(new char[] { ' ' }, 2)[1];
                    if (!channel.StartsWith("#"))
                        channel = '#' + channel;

                    IList<SteamID> channelSubs;
                    subscribers.TryGetValue(channel, out channelSubs);
                    if (channelSubs == null)
                        return;

                    channelSubs.Remove(Sender);
                    if (channelSubs.Count == 0)
                    {
                        subscribers.Remove(channel);
                        twitchBot.Leave(channel);
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
                        if (admin == Sender)
                        {
                            adminMessage += "my favorite master :steamhappy:";
                        }
                        else
                        {
                            adminMessage += "a master";
                        }
                    }
                    
                    adminMessage += "\n" + adminList.Count + " master(s) total";

                    steamBot.SendChatMessage(Sender, adminMessage);
                }
                #endregion
                #region say
                else if(lowerMessage.StartsWith("say "))
                {                    
                    if (adminList.Contains(Sender))
                    {
                        String[] data = Message.Split(new char[] { ' ' }, 3);
                        String channel = data[1].ToLower();

                        if (!channel.StartsWith("#"))
                            channel = "#" + channel;

                        if (data.Length == 3)
                        {

                            if (subscribers.Keys.Contains(channel) && subscribers[channel].Contains(Sender))
                            {
                                twitchBot.SendMessage(channel, data[2]);
                            }
                            else
                            {
                                steamBot.SendChatMessage(Sender, "You're not even part of that channel :steamfacepalm:");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(Sender, "Yeah, but WHAT should I say?");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(Sender, "I DON'T WANNA :steammocking:");
                    }
                }
                #endregion
                #region whisper
                else if (lowerMessage.StartsWith("whisper "))
                {
                    if (adminList.Contains(Sender))
                    {
                        String[] data = Message.Split(new char[] { ' ' }, 3);
                        String user = data[1].ToLower();

                        if (data.Length == 3)
                        {
                            SteamID target = new SteamID(user);
                            if (target.IsValid)
                            {
                                if (steamBot.IsFriend(target))
                                {
                                    steamBot.SendChatMessage(target, "My master " + steamBot.SteamIdToName(Sender) + " wanted my to tell you \"" + data[2] + "\"");
                                }
                                else
                                {
                                    steamBot.SendChatMessage(Sender, "That user is not my friend :steamsad:");
                                }
                            }
                            else
                            {
                                steamBot.SendChatMessage(Sender, "That's not a valid SteamID :/");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(Sender, "Yeah, but WHAT should I say?");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(Sender, "I DON'T WANNA :steammocking:");
                    }
                }
                #endregion
                #region dance
                else if (lowerMessage.Equals("dance"))
                {
                    steamBot.SendChatMessage(Sender, "TIME TO DAAAAAANCE\n<('.'<) (>'.')> (^'.')> <('.'^)\nhttps://www.youtube.com/watch?v=m6flHkC7oGs");
                }
                #endregion
                #region usage
                else if (new String[] { "help", "halp", "usage", "--help", "/?", "wtf" }.Contains(lowerMessage))
                {
                    steamBot.SendChatMessage(Sender, usage);
                }                
                else
                {
                    steamBot.SendChatMessage(Sender, Message + " to you too!");
                    steamBot.SendChatMessage(Sender, "Did you mean 'help'?");
                    //steamBot.SendChatMessage(Sender, usage);
                }
                #endregion

                //Console.WriteLine(Sender.Render() + ": " + callback.Message);
                Console.WriteLine(steamBot.SteamIdToName(Sender) + ": " + Message);

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
