using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sharkbite.Irc;

namespace Twitch2Steam
{
    public class TwitchBot : IDisposable
    {
        private Connection connection;
        public event PublicMessageEventHandler OnPublicMessage;

        public TwitchBot()
        {        
            //messageHandler += delegate(String channel, String user, String message) { Console.WriteLine(user + ": " + message); };

            string server = "irc.twitch.tv";
            string nick = "YourTwitchNameHere";
            string pw = "oauth:YourOauthKeyHere";

            ConnectionArgs cargs = new ConnectionArgs(nick, server);
            cargs.ServerPassword = pw;
            //cargs.Port = 6697; //SSL

            //When creating a Connection two additional protocols may be
            //enabled: CTCP and DCC. In this example we will disable them
            //both.
            connection = new Connection(cargs, false, false);		

            connection.Listener.OnRegistered += new RegisteredEventHandler(OnRegistered);

            //Listen for any messages sent to the channel
            //connection.Listener.OnPublic += new PublicMessageEventHandler(OnPublic);
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
            connection.Sender.Join(channel);
        }

        public void Leave(String channel)
        {
            connection.Sender.Part(channel);
        }

        private void onJoin(UserInfo user, string channel)
        {
            Console.WriteLine(user.Nick + " joined " + channel);
        }

        private void OnQuit(UserInfo user, string reason)
        {
            Console.WriteLine(user.Nick + " quit");
        }

        void OnPart(UserInfo user, string channel, string reason)
        {
            if(reason == "")
                Console.WriteLine(user.Nick + " parted from " + channel);
            else
                Console.WriteLine(user.Nick + " parted from " + channel + " because " + reason);
        }

        void OnPing(String message)
        {
            Console.WriteLine(message);
        }

        void Listener_OnInfo(string message, bool last)
        {
            Console.WriteLine(message);
        }

        public void Exit()
        {           
            if(connection.Connected)
                connection.Disconnect("Goodbye Cruel World");
        }

        public void OnRegistered()
        {
            Console.WriteLine("OnRegistered()");
            //We have to catch errors in our delegates because Thresher purposefully
            //does not handle them for us. Exceptions will cause the library to exit if they are not
            //caught.
            try
            {
                //The connection is ready so lets join a channel.
                //We can join any number of channels simultaneously but
                //one will do for now.
                //All commands are sent to IRC using the Sender object
                //from the Connection.
                //connection.Sender.Join("#CruelCow");
                //connection.Sender.Join("#cruelcow");
                //connection.Sender.Join("#trumpsc");
                //connection.Sender.
                //connection.Sender.PublicMessage("CruelCow", "Hashtags suck");
                //connection.Sender.PublicMessage("#CruelCow", "I connected! PogChamp ");
                connection.Sender.PrivateMessage("cruelcow", "I learned to whisper! " + DateTime.Now.ToShortTimeString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in OnRegistered(): " + e);
            }
        }

        public void OnPrivate(UserInfo user, string message)
        {
            Console.WriteLine(user.Nick + " whispered to me: " + message);
            //Quit IRC if someone sends us a 'die' message
            if (message == "die" && user.Nick.ToLower().Equals("cruelcow"))
            {
                connection.Disconnect("Bye");
            }
        }

        public void OnError(ReplyCode code, string message)
        {
            //All anticipated errors have a numeric code. The custom Thresher ones start at 1000 and
            //can be found in the ErrorCodes class. All the others are determined by the IRC spec
            //and can be found in RFC2812Codes.
            Console.WriteLine("An error of type " + code + " due to " + message + " has occurred.");
        }

        public void OnDisconnected()
        {
            //If this disconnection was involutary then you should have received an error
            //message ( from OnError() ) before this was called.
            Console.WriteLine("Connection to the server has been closed.");
        }

        public void Dispose()
        {
            Exit();
        }
    }
}
