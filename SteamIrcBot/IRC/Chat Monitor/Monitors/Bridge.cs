using SteamKit2;
using SteamKit2.Unified.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamIrcBot
{
    class Bridge : BaseMonitor
    {
        public Bridge()
        {
            Steam.Instance.CallbackManager.Subscribe<SteamUnifiedMessages.ServiceMethodNotification>(OnServiceMethod);
        }

        private void OnServiceMethod(SteamUnifiedMessages.ServiceMethodNotification callback)
        {
            Log.WriteInfo("Bridge", $"{callback.MethodName}");
            switch (callback.MethodName)
            {
                //ge: ChatRoomClient.NotifyIncomingChatMessage#1
                case "ChatRoomClient.NotifyIncomingChatMessage#1":
                    CChatRoom_IncomingChatMessage_Notification body = (CChatRoom_IncomingChatMessage_Notification)callback.Body;
                    Log.WriteDebug("Bridge", $"({body.chat_group_id}-{body.chat_id}), <{body.steamid_sender}> {body.message}");
                    if (body.chat_group_id == 17013 && body.chat_id == 289194)
                    {
                        IRC.Instance.Send("#test", $"<{body.steamid_sender}> {body.message}");
                    }// else
                    //{
                    //    IRC.Instance.Send("#test", $"Elsewhere({body.chat_group_id}-{body.chat_id}), <{body.steamid_sender}> {body.message}");
                    //}

                    break;
            }
        }

        protected override void OnMessage(MessageDetails msgDetails)
        {
            // TODO: Do the thing
            var request = new CChatRoom_SendChatMessage_Request
            {
                chat_group_id = 17013, // must be valid
                chat_id = 289194, // must be valid
                message = $"<{msgDetails.Sender}> {msgDetails.Message}"
            };

            Steam.Instance.Unified.SendMessage("ChatRoom.SendChatMessage#1", request);
        }
    }
}
