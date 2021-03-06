[![Build status](https://ci.appveyor.com/api/projects/status/ci4chkjeur07w87c?svg=true)](https://ci.appveyor.com/project/CruelCow/twitch2steam) [![Build Status](https://travis-ci.org/CruelCow/Twitch2Steam.svg)](https://travis-ci.org/CruelCow/Twitch2Steam)
#Twitch2Steam

Twitch2Steam aka TwitchRelay is a bot which listens to messages in Twitch Chat and forwards them via Steam chat. This allows streamers to follow their chat without needing a second monitor. 

##Usage

Installation is stricly speaking not required, a single instance can service multiple users. The "official" instance can be found at https://steamcommunity.com/id/TwitchRelayBot/ and freely used, though currently it will be rarely up. 
Adding the Bot to your steam friends and telling it which channels to follow is all that is required for usage. The bot will explain available commands when it receives "help" as a steam message.

Giving the bot moderator status in the Twitch channel is not required.

##Status 

This bot is still in early development, use with caution

##Building

This project references [SteamKit](https://github.com/SteamRE/SteamKit), [Thresher IRC](http://thresher.sourceforge.net/) and [log4net](https://logging.apache.org/log4net/). Testing is done via [NUnit](http://www.nunit.org/). All references are included in the /lib/ folder.

##Affiliation

This project is not affiliated with either Twitch nor Valve/Steam 

##Licence

This project is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE Version 3 (AGPL). ([General explanation](https://www.gnu.org/licenses/why-affero-gpl.html)) A copy of the license is included at the root of the repository in the file LICENSE.txt
