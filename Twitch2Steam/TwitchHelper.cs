using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;

namespace Twitch2Steam
{
    class TwitchHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TwitchHelper));

        private static readonly Random rand = new Random();

        //Example JSON file:
        //{"memberships":[{"room":{"irc_channel":"_cruelcow_1447033529452","owner_id":32132653,"display_name":"dsafsasad","public_invites_enabled":true,"cluster":"group","servers":["10.1.222.247:443","10.1.222.247:443","10.1.222.247:80","192.16.64.180:443","192.16.64.180:443","192.16.64.180:80","192.16.64.212:443","192.16.64.212:443","192.16.64.212:80","199.9.253.119:443","199.9.253.119:443","199.9.253.119:80","199.9.253.120:443","199.9.253.120:443","199.9.253.120:80"],"chatters_list_url":"http://tmi-groups.twitch.tv/group/user/_cruelcow_1447033529452/chatters"},"user":{"id":104937101},"is_owner":false,"is_mod":false,"is_confirmed":false,"is_banned":false,"created_at":1447004756}]}

        public static IPEndPoint getRandomGroupChatServer()
        {
            String url = "https://chatdepot.twitch.tv/room_memberships?oauth_token=";
            url += Settings.Default.IrcPassword.Substring(6);

            var s = new WebClient().DownloadString(url);

            var data = JObject.Parse(s);

            var servers = data["memberships"].First.First.First["servers"].Select(token => parse(( string )token));
            
            //parse returns invalid servers as null, filter them out
            //Twitch just throws each server in there 3 times with the ports 80/443/443. 
            //80 will not work, any we prefer 443 (encrypted) over 443, so filter to these results
            var validServers = servers.Where(t => t != null && t.Port == 443).ToArray();

            var server = validServers[rand.Next(validServers.Length)];

            log.Info($"Received {validServers.Length} GroupChat servers");

            return server;
        }

        private static IPEndPoint parse(String s)
        {
            Uri url;
            IPAddress ip;
            if (Uri.TryCreate($"http://{s}", UriKind.Absolute, out url) && IPAddress.TryParse(url.Host, out ip))
            {
                IPEndPoint endPoint = new IPEndPoint(ip, url.Port);
                return endPoint;
            }
            return null;
        }
    }
}
