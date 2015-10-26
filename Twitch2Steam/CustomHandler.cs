
using System;

using SteamKit2;
using SteamKit2.Internal; // this namespace stores the generated protobuf message structures

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Twitch2Steam
{
    
    /// <summary>
    /// This class handles Messages for which SteamKit2 doesn't offer a 'nice' interface.
    /// Spefically, it handles offline messages. Note that SteamBot.cs triggers those 
    /// messages to be sent in OnLoggedOn.
    /// </summary>
    public class CustomHandler : ClientMsgHandler
    {
        public delegate void OfflineMessageEventHandler(CMsgClientFSGetFriendMessageHistoryResponse messages);
        public event OfflineMessageEventHandler OnOfflineMessage;

        public override void HandleMsg(IPacketMsg packetMsg)
        {
            // this function is called when a message arrives from the Steam network
            // the SteamClient class will pass the message along to every registered ClientMsgHandler

            // the MsgType exposes the EMsg (type) of the message

            switch (packetMsg.MsgType)
            {
                case EMsg.ClientFSGetFriendMessageHistoryResponse:
                    var data = new ClientMsgProtobuf<CMsgClientFSGetFriendMessageHistoryResponse>(packetMsg).Body;
                    if (OnOfflineMessage != null)
                        OnOfflineMessage.Invoke(data);
                    foreach (var elem in data.messages)
                    {
                        if (elem.unread)
                        {
                            Console.WriteLine("I MISSED a message from " + elem.accountid + ": " + elem.message);
                        }
                    }
                    break;
        
                    //Packets which are neither unexpected nor handled in this class
                case EMsg.ChannelEncryptRequest:
                case EMsg.ChannelEncryptResult:
                case EMsg.ClientServersAvailable:
                case EMsg.ClientLogOnResponse:
                case EMsg.ClientAccountInfo:
                case EMsg.ClientEmailAddrInfo:
                case EMsg.ClientVACBanStatus:
                case EMsg.ClientFriendsList:
                case EMsg.ClientFriendsGroupsList:
                case EMsg.ClientPlayerNicknameList:
                case EMsg.ClientLicenseList:
                case EMsg.ClientUpdateGuestPassesList:
                case EMsg.ClientWalletInfoUpdate:
                case EMsg.ClientGameConnectTokens:
                case EMsg.ClientSessionToken:
                case EMsg.ClientIsLimitedAccount:
                case EMsg.ClientCMList:
                case EMsg.ClientServerList:
                case EMsg.ClientRequestedClientStats:
                case EMsg.Multi:
                case EMsg.ClientPersonaState:
                case EMsg.ClientPersonaChangeResponse:
                case EMsg.ClientNewLoginKey:
                case EMsg.ClientFriendMsgIncoming:
                case EMsg.ClientFSOfflineMessageNotification:
                    break;                

                case EMsg.ClientMarketingMessageUpdate2:
                    //TODO mark as read.
                    break;


                default: //"Unusual" packet, might be interesting
                    Console.WriteLine("->" + packetMsg.MsgType);
                    
                    break;
            }
        }
    }
}