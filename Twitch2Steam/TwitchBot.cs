﻿using System;
using Sharkbite.Irc;
using log4net;
using System.Timers;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;

namespace Twitch2Steam
{
    public class TwitchBot : IDisposable
    {
        private Connection connection;
        private readonly Timer heartbeatMonitor;
        private readonly Timer reconnectTimer;
        private readonly ExponentialBackoff reconnectBackoff;

        private readonly ISet<String> channelList;

        private readonly ILog log = LogManager.GetLogger(typeof(TwitchBot));

        private readonly Random random = new Random();

        public event PublicMessageEventHandler OnPublicMessage;

        public TwitchBot()
        {
            channelList = new HashSet<String>();
            var twitchServer = TwitchHelper.getRandomGroupChatServer();
            
            heartbeatMonitor = new Timer(TimeSpan.FromSeconds(60).TotalMilliseconds);
            heartbeatMonitor.AutoReset = true;
            heartbeatMonitor.Elapsed += HeartbeatMonitor_Elapsed;
            heartbeatMonitor.Start();
            
            reconnectTimer = new Timer();
            reconnectTimer.AutoReset = false;
            reconnectTimer.Elapsed += ReconnectTimer_Elapsed;

            reconnectBackoff = new ExponentialBackoff();

            Init_Connection(twitchServer);

            try
            {
                connection.Connect();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is SocketException)
            {
                OnDisconnected();
            }
        }

        private void Init_Connection(IPEndPoint twitchServer)
        {
            ConnectionArgs cargs = new ConnectionArgs(Settings.Default.IrcName, twitchServer.Address.ToString())
            {
                Port = twitchServer.Port,
                ServerPassword = Settings.Default.IrcPassword
            };

            log.Info($"Trying to connect to {cargs.Hostname} on Port {cargs.Port} as {Settings.Default.IrcName}");

            connection = new Connection(cargs, false, false);

            connection.Listener.OnRegistered += new RegisteredEventHandler(OnRegistered);

            connection.Listener.OnPublic += delegate ( UserInfo user, string channel, string message )
            {
                if (OnPublicMessage != null)
                    OnPublicMessage.Invoke(user, channel, message);
            };

            //Listen for bot commands sent as private messages
            connection.Listener.OnPrivate += new PrivateMessageEventHandler(OnPrivate);

            //Listen for notification that an error has ocurred 
            connection.Listener.OnError += new ErrorMessageEventHandler(OnError);

            //Listen for notification that we are no longer connected.
            connection.Listener.OnDisconnected += new DisconnectedEventHandler(OnDisconnected);
            connection.Listener.OnDisconnecting += new DisconnectingEventHandler(OnDisconnecting);

            connection.Listener.OnPing += new PingEventHandler(OnPing);
            connection.Listener.OnJoin += new JoinEventHandler(onJoin);
            connection.Listener.OnPart += new PartEventHandler(OnPart);
            connection.Listener.OnQuit += new QuitEventHandler(OnQuit);
            connection.Listener.OnInfo += new InfoEventHandler(OnInfo);

            //Join("#" + cargs.UserName.ToLower());
        }


        private void ReconnectTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (connection.Connected)
            {
                log.Error("Already connected");
                return;
            }

            try
            {
                if (reconnectBackoff.IsDelayMaxed || connection.ConnectionData.Hostname == IPAddress.None.ToString())
                {
                    log.Info("Getting a new server");
                    var server = TwitchHelper.getRandomGroupChatServer();
                    Init_Connection(server);
                    reconnectBackoff.Reset();
                }

                log.Debug("Trying to reconnect");

                connection.Connect();
                log.Info("Successfully reconnected");


                //Ensure we rejoin all our channels
                log.Debug($"Rejoining {channelList.Count} channels");
                foreach (var channel in channelList)
                {
                    connection.Sender.Join(channel);
                }


                //If Enabled and AutoReset are both set to false, and the timer has previously been enabled,
                //setting the Interval property causes the Elapsed event to be raised once, as if the Enabled 
                //property had been set to true. To set the interval without raising the event, you can 
                //temporarily set the Enabled property to true, set the Interval property to the desired time 
                //interval, and then immediately set the Enabled property back to false.
                // -- https://msdn.microsoft.com/en-us/library/system.timers.timer.interval%28v=vs.110%29.aspx

                reconnectTimer.Enabled = true;
                reconnectTimer.Interval = reconnectBackoff.Reset().TotalMilliseconds;
                reconnectTimer.Enabled = false;
            }
            catch (SocketException ex)
            {
                TimeSpan delay = reconnectBackoff.NextDelay;
                reconnectTimer.Interval = delay.TotalMilliseconds;
                log.Error($"Reconnect has failed: {ex.Message}. Trying again in {delay.ToReadableString()}.");
                reconnectTimer.Start();
            }
        }

        private void HeartbeatMonitor_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (connection.Connected)
            {
                //connection.Sender.Names("#" + connection.ConnectionData.Nick.ToLower());
                //this.SendMessage("#" + connection.ConnectionData.Nick.ToLower(), "heartbeat");
                connection.Sender.PrivateMessage(connection.ConnectionData.Nick, "heartbeat " + DateTime.Now.ToString("s"));
            }
        }

        public void Join(String channel)
        {
            log.Info($"Joining {channel}");
            connection.Sender.Join(channel);
            channelList.Add(channel);
        }

        public void Leave(String channel)
        {
            log.Info($"Leaving {channel}");
            connection.Sender.Part(channel);
            channelList.Remove(channel);
        }

        private void onJoin(UserInfo user, string channel)
        {
            log.Debug($"{user.User} joined {channel}");
        }

        private void OnQuit(UserInfo user, string reason)
        {
            log.Debug($"{user} quit ({reason ?? "no reason supplied"})");
        }

        void OnPart(UserInfo user, string channel, string reason)
        {
            log.Debug($"{user} parted from {channel} ({reason ?? "no reason supplied"})");
        }

        void OnPing(String message)
        {
            log.Debug($"Ping: {message}");
        }

        void OnInfo(string message, bool last)
        {
            log.Info($"Info: {message}");
        }

        private bool userInitiated = false;
        public void Exit()
        {
            heartbeatMonitor.Stop();
            userInitiated = true;
            if(connection.Connected)
                connection.Disconnect("Goodbye Cruel World");
        }

        public void OnRegistered()
        {
            log.Info("Successfully connected");
        }

        public void Whisper(String user, String message)
        {
            //connection.Sender.PublicMessage("#cruelcow", "/w cruelcow I can whisper now!");
            //connection.Sender.PrivateMessage("jtv", "/w cruelcow message2");
            //Either are valid syntax, but only if connected to a "group chat" server
            //Getting group chat server: http://blog.bashtech.net/twitch-group-chat-irc/
            //"We purposely made whispers convoluted for IRC integration..."
            //https://discuss.dev.twitch.tv/t/whispers-on-irc/2459/

            connection.Sender.PrivateMessage("jtv", $"/w {user} {message}");
        }

        public void OnPrivate(UserInfo user, string message)
        {
            log.Debug($"IrC user {user} whispered: {message} ");
        }

        public void OnError(ReplyCode code, string message)
        {
            //All anticipated errors have a numeric code. The custom Thresher ones start at 1000 and
            //can be found in the ErrorCodes class. All the others are determined by the IRC spec
            //and can be found in RFC2812Codes.

            log.Error("An error of type " + code + " due to " + message + " has occurred.");
        }

        public void OnDisconnected()
        {
            //If this disconnection was involutary then you should have received an error
            //message ( from OnError() ) before this was called.
            if (userInitiated)
            {
                log.Info("Connection to the server has been closed.");
            }
            else
            {
                log.Error("Connection to the server has been closed.");
                reconnectTimer.Start();
            }
        }

        public void OnDisconnecting()
        {
            log.Info("Disconnecting from twitch.");
        }

        public void Dispose() //TODO fix
        {
            Exit();
        }

        public void SendMessage(String channel, String text)
        {
            connection.Sender.PublicMessage(channel, text);
        }
    }
}
