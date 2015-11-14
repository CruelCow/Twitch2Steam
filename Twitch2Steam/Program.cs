using Sharkbite.Irc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twitch2Steam
{
    class Program
    {
        static void Main(string[] args)
        {
            LogTester.Test();
            using (var steamBot = new SteamBot())
            using (var twitchBot = new TwitchBot())
            {
            //    var twitchBot = new TwitchBot2();
                Glue glue = new Glue(twitchBot, steamBot);
            }
        }
    }
}
