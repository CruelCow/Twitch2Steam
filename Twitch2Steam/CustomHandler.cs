
using System;

using SteamKit2;
using SteamKit2.Internal; // this namespace stores the generated protobuf message structures

using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace Twitch2Steam
{
    
    /// <summary>
    /// This class marks "expected" messages and prints about others. This can be useful for debugging.
    /// </summary>
    public class CustomHandler : ClientMsgHandler
    {
        private readonly ILog log = LogManager.GetLogger(typeof(CustomHandler));

        public override void HandleMsg(IPacketMsg packetMsg)
        {
            switch (packetMsg.MsgType)
            {
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
                case EMsg.ClientFSGetFriendMessageHistoryResponse:
                case EMsg.ClientChatInvite:
                case EMsg.ClientFriendMsgEchoToSender:
                case EMsg.ClientUpdateMachineAuth:
                    break;

                case EMsg.ClientMarketingMessageUpdate2:
                    //TODO mark as read.
                    break;
                    
                default: //"Unusual" packet, might be interesting
                    log.Warn($"Unusual message {packetMsg.MsgType}");
                    break;
            }
        }
    }
}