﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Twitch2Steam.Properties;
using Sharkbite.Irc;
using log4net;

namespace Twitch2Steam
{
    public class TwitchBot : IDisposable
    {
        private Connection connection;
        private readonly ILog log = LogManager.GetLogger(typeof(TwitchBot));

        public event PublicMessageEventHandler OnPublicMessage;

        public TwitchBot()
        {
            ConnectionArgs cargs = new ConnectionArgs(Settings.Default.IrcName, Settings.Default.IrcServer);
            cargs.Port = Settings.Default.Port;
            cargs.ServerPassword = Settings.Default.IrcPassword;

            log.Info($"Trying to connect to {cargs.Hostname}:{cargs.Port} as {Settings.Default.IrcName}");

            connection = new Connection(cargs, false, false);		

            connection.Listener.OnRegistered += new RegisteredEventHandler(OnRegistered);

            connection.Listener.OnPublic += delegate(UserInfo user, string channel, string message) 
                { 
                    if(OnPublicMessage != null)
                        OnPublicMessage.Invoke(user, channel, message); 
                };

            //Listen for bot commands sent as private messages
            connection.Listener.OnPrivate += new PrivateMessageEventHandler(OnPrivate);

            //Listen for notification that an error has ocurred 
            connection.Listener.OnError += new ErrorMessageEventHandler(OnError);

            //Listen for notification that we are no longer connected.
            connection.Listener.OnDisconnected += new DisconnectedEventHandler(OnDisconnected);

            connection.Listener.OnPing += new PingEventHandler(OnPing);
            connection.Listener.OnJoin += new JoinEventHandler(onJoin);
            connection.Listener.OnPart += new PartEventHandler(OnPart);
            connection.Listener.OnQuit += new QuitEventHandler(OnQuit);

            //connection.Listener.OnInfo += new OnInfoEventHandler( Listener_OnInfo);

            connection.Connect();
        }

        public void Join(String channel)
        {
            log.Info($"Joining {channel}");
            connection.Sender.Join(channel);
        }

        public void Leave(String channel)
        {
            log.Info($"Leaving {channel}");
            connection.Sender.Part(channel);
        }

        private void onJoin(UserInfo user, string channel)
        {
            log.Debug($"{user} joined {channel}");
            //Console.WriteLine(user.Nick + " joined " + channel);
        }

        private void OnQuit(UserInfo user, string reason)
        {
            log.Debug($"{user} quit ({reason ?? "no reason supplied"})");
            //Console.WriteLine(user.Nick + " quit");
        }

        void OnPart(UserInfo user, string channel, string reason)
        {
            log.Debug($"{user} parted from {channel} ({reason ?? "no reason supplied"})");
            /*if(reason == "")
                Console.WriteLine(user.Nick + " parted from " + channel);
            else
                Console.WriteLine(user.Nick + " parted from " + channel + " because " + reason);*/
        }

        void OnPing(String message)
        {
            log.Debug($"Ping: {message}");
        }

        void Listener_OnInfo(string message, bool last)
        {
            log.Info($"Info: {message}");
        }

        public void Exit()
        {           
            if(connection.Connected)
                connection.Disconnect("Goodbye Cruel World");
        }

        public void OnRegistered()
        {
            log.Info("Successfully connected");
            //Console.WriteLine("OnRegistered()");
            //We have to catch errors in our delegates because Thresher purposefully
            //does not handle them for us. Exceptions will cause the library to exit if they are not
            //caught.
            try
            {
                connection.Sender.PrivateMessage("cruelcow", "I learned to whisper! " + DateTime.Now.ToShortTimeString());
            }
            catch (Exception e)
            {
                log.Error("Unable to send private message", e);
                //Console.WriteLine("Error in OnRegistered(): " + e);
            }
        }

        public void OnPrivate(UserInfo user, string message)
        {
            log.Debug($"IrC user {user} whispered: {message} ");
            //Console.WriteLine(user.Nick + " whispered to me: " + message);
            //Quit IRC if master sends us a 'die' message
            /*if (message == "die" && user.Nick.ToLower().Equals("cruelcow"))
            {
                connection.Disconnect("Bye");
            }*/
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
            log.Info("Connection to the server has been closed.");
        }

        public void Dispose()
        {
            Exit();
        }

        public void SendMessage(String channel, String text)
        {
            connection.Sender.PublicMessage(channel, text);
        }
    }
}
