using UnityEngine;
using System.Collections;
using Byn.Net;
using System.Collections.Generic;

namespace UnityVOIP
{
    public class VoiceChatUnityServer : MonoBehaviour
    {
        P2PServer server;
        public string serverURL = "wss://nameless-scrubland-88927.herokuapp.com";
        public string roomName = "voicechattest";
        void Start()
        {
            server = new P2PServer(serverURL, roomName);
            server.OnReceivedMessage += Server_OnReceivedMessage;
        }

        private void Server_OnReceivedMessage(NetworkEvent message)
        {
            byte[] messageBytes = message.GetDataAsByteArray();
            lock(server.peers)
            {
                foreach (KeyValuePair<string, ConnectionId> peer in server.peers)
                {
                    if (peer.Value != message.ConnectionId)
                    {
                        server.SendMessage(peer.Value, messageBytes, false);
                    }
                }
            }
        }

        private void Update()
        {
            server.UpdateServer();
        }
    } 
}
