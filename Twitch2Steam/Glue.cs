using Sharkbite.Irc;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twitch2Steam;

namespace Twitch2Steam
{
    public class Glue
    {
        private readonly Object myLock; //TODO my fine grained lock
        private readonly TwitchBot twitchBot;
        private readonly SteamBot steamBot;
        private readonly Dictionary<String, IList<SteamID>> subscribers;

        private readonly SteamID[] admins = new SteamID[] { new SteamID(76561197991095468) };

        public Glue(TwitchBot twitchBot, SteamBot steamBot)
        {
            this.twitchBot = twitchBot;
            this.steamBot = steamBot;
            subscribers = new Dictionary<String, IList<SteamID>>();
            myLock = new Object();

            twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { Console.WriteLine(user.Nick + ": " + message); };
            //twitchBot.OnPublicMessage += delegate(UserInfo user, String channel, String message) { steamBot.Broadcast(user.Nick + ": " + message); };
            twitchBot.OnPublicMessage += OnTwitchPublicMessage;
            steamBot.OnFriendMessage += OnSteamFriendMessage;

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
                    return;

                String lowerMessage = callback.Message.ToLower();

                if (lowerMessage == "die") 
                {
                    bool isAdmin = false;
                    
                    foreach (var admin in admins)
                    {
                        if (admin.ConvertToUInt64() == callback.Sender.ConvertToUInt64())
                        {
                            isAdmin = true;
                        }
                    }
                    if (isAdmin)
                    {
                        steamBot.SendChatMessage(callback.Sender, "OKIDOKI! *explodes*");
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

                    channels = "You are subscribed to the following channels: " + channels;

                    steamBot.SendChatMessage(callback.Sender, channels);
                }
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
                }
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
                else
                {
                    //steamBot.SendChatMessage(callback.Sender, "\"" + callback.Message + "\" to you too!");
                    steamBot.SendChatMessage(callback.Sender,
                        "\nCommands:" +
                        "\n\tsubscribe <TwitchChannel> \t send all messages of this channel to you. '#' optional" +
                        "\n\tunsubscribe <TwitchChannel> \t stop sending all messages of this channel to you. '#' optional" +
                        "\n\tshut up \t\t\t\t\t Unsubscribe from all channels" +
                        "\n\tlist \t\t\t\t\t\t show all channels currently subscribed to" +
                        "\n\tdie \t\t\t\t\t\t Shuts down the bot"
                        );
                        
                }
                
                //Console.WriteLine(callback.Sender.Render() + ": " + callback.Message);
                Console.WriteLine(steamBot.SteamIdToName(callback.Sender) + ": " + callback.Message);

            }
        }

        public void Exit()
        {            
            twitchBot.Exit();
            steamBot.Exit();
        }


        

    }
}
