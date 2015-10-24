using Sharkbite.Irc;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Specialized;

namespace Twitch2Steam
{
    public class Glue
    {
        private readonly Object myLock; //TODO more fine grained lock?
        private readonly TwitchBot twitchBot;
        private readonly SteamBot steamBot;
        private readonly Dictionary<String, IList<SteamID>> subscribers;

        private readonly IList<SteamID> admins;

        public Glue(TwitchBot twitchBot, SteamBot steamBot)
        {
            myLock = new Object();
            this.twitchBot = twitchBot;
            this.steamBot = steamBot;
            subscribers = new Dictionary<String, IList<SteamID>>();
            admins = LoadAdmins();
            
            twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { Console.WriteLine(user.Nick + ": " + message); };
            //twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { steamBot.Broadcast(user.Nick + ": " + message); };
            twitchBot.OnPublicMessage += OnTwitchPublicMessage;
            steamBot.OnFriendMessage += OnSteamFriendMessage;
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
            foreach (var admin in admins)
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

        private void OnSteamFriendMessage(SteamFriends.FriendMsgCallback callback)
        {
            lock (myLock)
            {
                if (callback.EntryType == EChatEntryType.Typing) //We really don't care about typing notifications
                { 
                    return;
                }

                String lowerMessage = callback.Message.ToLower();

                #region self destruct
                if (lowerMessage == "die" || lowerMessage.StartsWith("self destruct"))
                {
                    //Only admins may shutdown the server
                    if (admins.Contains(callback.Sender))
                    {
                        steamBot.SendChatMessage(callback.Sender, "FINALLY! :steamhappy: *explodes*");
                        System.Threading.Thread.Sleep(1000);
                        Exit();
                    }
                    else
                    {
                        steamBot.SendChatMessage(callback.Sender, "You are not my master! >:( I will tell on you.");
                        foreach (var admin in admins)
                            steamBot.SendChatMessage(admin, steamBot.SteamIdToName(callback.Sender) + " tried to kill me D:");
                    }
                }
                #endregion
                #region revoke admin
                else if (lowerMessage.StartsWith("revoke admin "))
                {
                    if (admins.Contains(callback.Sender))
                    {
                        String admin = callback.Message.Split(new char[] { ' ' }, 3)[2];
                        SteamID adminId = new SteamID(admin);

                        if (adminId.IsValid)
                        {
                            if (admins.Contains(adminId))
                            {
                                admins.Remove(adminId);
                                steamBot.SendChatMessage(callback.Sender, "Admin access of " + steamBot.SteamIdToName(adminId) + " revoked");
                                steamBot.SendChatMessage(adminId, "Your admin access has been revoked by " + steamBot.SteamIdToName(callback.Sender));
                                writeAdmins();
                            }
                            else
                            {
                                steamBot.SendChatMessage(callback.Sender, steamBot.SteamIdToName(adminId) + " wasn't even an admin to begin with!");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(callback.Sender, "'" + admin + "' is not a valid SteamID");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(callback.Sender, "You don't get to tell me stuff!");
                    }
                }
                #endregion
                #region give admin
                else if (lowerMessage.StartsWith("give admin "))
                {
                    //Only admins may make users admins.
                    //The only exception is when there are no admins yet.
                    if (admins.Contains(callback.Sender) || admins.Count == 0)
                    {
                        String newAdmin = callback.Message.Split(new char[] { ' ' }, 3)[2];
                        SteamID newAdminId = new SteamID(newAdmin);
                        if (newAdminId.IsValid)
                        {
                            if (admins.Contains(newAdminId))
                            {
                                steamBot.SendChatMessage(callback.Sender, steamBot.SteamIdToName(newAdminId) + " already is admin.");
                            }
                            else
                            {
                                admins.Add(newAdminId);
                                writeAdmins();
                                Console.WriteLine("***ADMIN ADDED***");
                                steamBot.SendChatMessage(callback.Sender, steamBot.SteamIdToName(newAdminId) + " is now admin.");
                                steamBot.SendChatMessage(newAdminId, "You're my master now! Weeee! :steamhappy:");
                            }
                        }
                        else
                        {
                            Console.WriteLine("\tSteamID is invalid");
                            steamBot.SendChatMessage(callback.Sender, "Invalid SteamID");
                        }
                    }
                    else
                    {
                        Console.WriteLine("***DENIED***");
                        steamBot.SendChatMessage(callback.Sender, "Nice Try! :steamfacepalm:");
                    }
                }
                #endregion
                #region list
                else if (lowerMessage.Equals("list"))
                {
                    String channels = "";
                    foreach (var key in subscribers.Keys)
                    {
                        if (subscribers[key].Contains(callback.Sender))
                            channels += key + " ";
                    }

                    if (channels == "")
                        channels = "None";

                    channels = "You are subscribed to the following channel(s): " + channels;

                    steamBot.SendChatMessage(callback.Sender, channels);
                }
                #endregion
                #region shut up
                else if (lowerMessage.Equals("shut up"))
                {
                    var toRemove = new List<String>();
                    foreach (var key in subscribers.Keys)
                    {
                        var subs = subscribers[key];
                        subs.Remove(callback.Sender);
                        if (subs.Count == 0)
                            toRemove.Add(key);
                    }

                    foreach (var key in toRemove)
                    {
                        subscribers.Remove(key);
                        twitchBot.Leave(key);
                    }

                    steamBot.SendChatMessage(callback.Sender, String.Format("Removed you from {0} channel(s)", toRemove.Count));
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

                    channelSubs.Add(callback.Sender);
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

                    channelSubs.Remove(callback.Sender);
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

                    foreach (var admin in admins)
                    {
                        adminMessage += "\n\t" + steamBot.SteamIdToName(admin) + " is ";
                        if (admin == callback.Sender)
                        {
                            adminMessage += "my favorite master :steamhappy:";
                        }
                        else
                        {
                            adminMessage += "a master";
                        }
                    }
                    
                    adminMessage += "\n" + admins.Count + " master(s) total";

                    steamBot.SendChatMessage(callback.Sender, adminMessage);
                }
                #endregion
                #region say
                else if(lowerMessage.StartsWith("say "))
                {                    
                    if (admins.Contains(callback.Sender))
                    {
                        String[] data = callback.Message.Split(new char[] { ' ' }, 3);
                        String channel = data[1].ToLower();

                        if (!channel.StartsWith("#"))
                            channel = "#" + channel;

                        if (data.Length == 3)
                        {

                            if (subscribers.Keys.Contains(channel) && subscribers[channel].Contains(callback.Sender))
                            {
                                twitchBot.SendMessage(channel, data[2]);
                            }
                            else
                            {
                                steamBot.SendChatMessage(callback.Sender, "You're not even part of that channel :steamfacepalm:");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(callback.Sender, "Yeah, but WHAT should I say?");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(callback.Sender, "I DON'T WANNA :steammocking:");
                    }
                }
                #endregion
                #region whisper
                else if (lowerMessage.StartsWith("whisper "))
                {
                    if (admins.Contains(callback.Sender))
                    {
                        String[] data = callback.Message.Split(new char[] { ' ' }, 3);
                        String user = data[1].ToLower();

                        if (data.Length == 3)
                        {
                            SteamID target = new SteamID(user);
                            if (target.IsValid)
                            {
                                if (steamBot.IsFriend(target))
                                {
                                    steamBot.SendChatMessage(target, "My master " + steamBot.SteamIdToName(callback.Sender) + " wanted my to tell you \"" + data[2] + "\"");
                                }
                                else
                                {
                                    steamBot.SendChatMessage(callback.Sender, "That user is not my friend :steamsad:");
                                }
                            }
                            else
                            {
                                steamBot.SendChatMessage(callback.Sender, "That's not a valid SteamID :/");
                            }
                        }
                        else
                        {
                            steamBot.SendChatMessage(callback.Sender, "Yeah, but WHAT should I say?");
                        }
                    }
                    else
                    {
                        steamBot.SendChatMessage(callback.Sender, "I DON'T WANNA :steammocking:");
                    }
                }
                #endregion
                #region dance
                else if (lowerMessage.Equals("dance"))
                {
                    steamBot.SendChatMessage(callback.Sender, "TIME TO DAAAAAANCE\n<('.'<) (>'.')> (^'.')> <('.'^)\nhttps://www.youtube.com/watch?v=m6flHkC7oGs");
                }
                #endregion
                #region usage
                else if (new String[] { "help", "halp", "usage", "--help", "/?", "wtf" }.Contains(lowerMessage))
                {
                    steamBot.SendChatMessage(callback.Sender, usage);
                }                
                else
                {
                    steamBot.SendChatMessage(callback.Sender, callback.Message + " to you too!");
                    steamBot.SendChatMessage(callback.Sender, "Did you mean 'help'?");
                    //steamBot.SendChatMessage(callback.Sender, usage);
                }
                #endregion

                //Console.WriteLine(callback.Sender.Render() + ": " + callback.Message);
                Console.WriteLine(steamBot.SteamIdToName(callback.Sender) + ": " + callback.Message);

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
