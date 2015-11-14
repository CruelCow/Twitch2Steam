using System;
using Sharkbite.Irc;
using log4net;
using System.Timers;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Diagnostics;

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

        public event PublicMessageEventHandler OnPublicMessage;

        public TwitchBot()
        {
            channelList = new HashSet<String>();

            ConnectionArgs cargs = new ConnectionArgs(Settings.Default.IrcName, Settings.Default.IrcServer)
            {
                Port = Settings.Default.Port,
                ServerPassword = Settings.Default.IrcPassword
            };

            log.Info($"Trying to connect to {cargs.Hostname}:{cargs.Port} as {Settings.Default.IrcName}");

            connection = new Connection(cargs, false, false);

            connection.Listener.OnRegistered += new RegisteredEventHandler(OnRegistered);

            connection.Listener.OnPublic += delegate (UserInfo user, string channel, string message)
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
            
            heartbeatMonitor = new Timer(TimeSpan.FromSeconds(60).TotalMilliseconds);
            heartbeatMonitor.AutoReset = true;
            heartbeatMonitor.Elapsed += HeartbeatMonitor_Elapsed;
            heartbeatMonitor.Start();


            reconnectTimer = new Timer();
            reconnectTimer.AutoReset = false;
            reconnectTimer.Elapsed += ReconnectTimer_Elapsed;

            reconnectBackoff = new ExponentialBackoff();
        }

        public void Connect()
        {
            try
            {
                connection.Connect();
            }
            catch (SocketException)
            {
                OnDisconnected();
            }
        }

        private void ReconnectTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                log.Debug("Trying to reconnect");
                if (connection.Connected)
                {
                    log.Warn("Already connected");
                    return;
                }
                connection.Connect();
                log.Debug("Successfully reconnected");

                log.Info($"Rejoining {channelList.Count} channel{(channelList.Count == 1 ? String.Empty : "s")}");
                foreach (var channel in channelList)
                    connection.Sender.Join(channel);

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
            bool added = channelList.Add(channel);
            Debug.Assert(added);
        }

        public void Leave(String channel)
        {
            log.Info($"Leaving {channel}");
            connection.Sender.Part(channel);
            bool removed = channelList.Remove(channel);
            Debug.Assert(removed);
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
            heartbeatMonitor.Dispose();

            reconnectTimer.Stop();
            reconnectTimer.Dispose();

            userInitiated = true;
            if(connection.Connected)
                connection.Disconnect("Goodbye Cruel World");
        }

        public void OnRegistered()
        {
            log.Info("Successfully connected");

            //Request Join/Part messages etc. 
            //Supposedly only works in channels with <10 users
            //Broken atm, might only work with group chat servers
            //https://github.com/justintv/Twitch-API/blob/master/IRC.md
            connection.Sender.Raw("CAP REQ :twitch.tv/membership");
        }

        public void OnPrivate(UserInfo user, string message)
        {
            log.Debug($"IrC user {user} whispered: {message} ");
        }

        public void OnError(ReplyCode code, string message)
        {
            //expected message for requesting membership info
            //IRC standards? What is that?
            if (message.Equals("tmi.twitch.tv CAP * ACK :twitch.tv/membership"))
                return; 

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

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(Boolean disposing)
        {
            if(disposing)
            {
                Exit();
            }
        }

        public void SendMessage(String channel, String text)
        {
            connection.Sender.PublicMessage(channel, text);
        }
    }
}
