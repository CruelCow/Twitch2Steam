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
            var sb = new SteamBot("YourSteamLoginNameHere", "YourSteamPasswordHere");
            var tb = new TwitchBot(sb);
            sb.loop();
            tb.Exit();
            sb.Exit();
        }
    }
}
