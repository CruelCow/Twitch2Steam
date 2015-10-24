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
            Console.WriteLine("Starting Bot");
            using (var steamBot = new SteamBot())
            using (var twitchBot = new TwitchBot())
            {
                Glue glue = new Glue(twitchBot, steamBot);

                steamBot.loop();
            }
            //tb.Exit();
            //sb.Exit();
        }
    }
}
