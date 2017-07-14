using Byn.Net;
using System;
using System.Collections.Generic;
using UnityEngine;

public class P2PClient : IDisposable
{
    public Dictionary<string, ConnectionId> peers;
    IBasicNetwork mNetwork = null;
    string roomName;

    public P2PClient(string signalingServer, string roomName)
    {
        this.roomName = roomName;
        peers = new Dictionary<string, ConnectionId>();
        //mNetwork = WebRtcNetworkFactory.Instance.CreateDefault("wss://nameless-scrubland-88927.herokuapp.com", new IceServer[] { new IceServer("stun:stun.l.google.com:19302") });
        mNetwork = WebRtcNetworkFactory.Instance.CreateDefault(signalingServer, new IceServer[] { new IceServer("stun:stun.l.google.com:19302") });
        if (mNetwork == null)
        {
            PrintDebug("failed to setup network");
            return;
        }

        mNetwork.Connect(roomName);
    }


    public delegate void ReceivedMessageCallback(NetworkEvent message);
    public event ReceivedMessageCallback OnReceivedMessage;

    public delegate void OnConnectionCallback(ConnectionId connectionId);
    public event OnConnectionCallback OnConnection;

    public delegate void OnDisconnectionCallback(ConnectionId connectionId);
    public event OnDisconnectionCallback OnDisconnection;


    public void SendMessage(ConnectionId connectionId, byte[] data, bool isReliable)
    {
        SendMessage(connectionId, data, 0, data.Length, isReliable);
    }

    public void SendMessage(ConnectionId connectionId, byte[] data, int dataOffset, int dataLen, bool isReliable)
    {
        if (mNetwork != null && peers.ContainsKey(connectionId.ToString()))
        {
            mNetwork.SendData(connectionId, data, dataOffset, dataLen, isReliable);
        }
        else if (mNetwork == null)
        {
            PrintDebug("Can't send message, network isn't initialized");
        }
        else
        {
            PrintDebug("Can't send message, connection id " + connectionId.ToString() + " is not a currently connected peer");
        }
    }

    public void SendMessageToAll(byte[] data, bool isReliable)
    {
        SendMessageToAll(data, 0, data.Length, isReliable);
    }

    public void SendMessageToAll(byte[] data, int dataOffset, int dataLen, bool isReliable)
    {
        if (mNetwork != null)
        {
            foreach (KeyValuePair<string, ConnectionId> peer in peers)
            {
                mNetwork.SendData(peer.Value, data, dataOffset, dataLen, isReliable);
            }
        }
        else
        {
            PrintDebug("Can't send message, network isn't initialized");
        }
    }

    private void Cleanup()
    {
        if (mNetwork != null)
        {
            mNetwork.Dispose();
            mNetwork = null;
        }
    }

    void PrintDebug(string message)
    {
        Debug.Log(message);
    }

    int frameCount = 0;

    public void UpdateClient()
    {

        frameCount++;

        if (peers.Count == 0 && frameCount % 10000 == 0)
        {
            mNetwork.Connect(roomName);
        }


        HandleNetwork();

    }


    void HandleNetwork()
    {
        //check if the network was created
        if (mNetwork != null)
        {
            //first update it to read the data from the underlaying network system
            mNetwork.Update();

            //handle all new events that happened since the last update
            NetworkEvent evt;
            //check for new messages and keep checking if mNetwork is available. it might get destroyed
            //due to an event
            while (mNetwork != null && mNetwork.Dequeue(out evt))
            {
                //check every message
                switch (evt.Type)
                {
                    case NetEventType.ServerInitialized:
                        {
                            //server initialized message received
                            //this is the reaction to StartServer -> switch GUI mode
                            string address = evt.Info;
                            PrintDebug("Server started. Address: " + address);
                            //uRoomName.text = "" + address;
                        }
                        break;
                    case NetEventType.ServerInitFailed:
                        {
                            //user tried to start the server but it failed
                            //maybe the user is offline or signaling server down?
                            PrintDebug("Server start failed.");
                        }
                        break;
                    case NetEventType.ServerClosed:
                        {
                            mNetwork.Connect(roomName);
                        }
                        break;
                    case NetEventType.NewConnection:
                        {
                            // mConnections.Add(evt.ConnectionId);
                            //either user runs a client and connected to a server or the
                            //user runs the server and a new client connected
                            PrintDebug("New local connection! ID: " + evt.ConnectionId);
                            peers[evt.ConnectionId.ToString()] = evt.ConnectionId;
                            if (OnConnection != null)
                            {
                                OnConnection(evt.ConnectionId);
                            }
                        }
                        break;
                    case NetEventType.ConnectionFailed:
                        {
                            //Outgoing connection failed. Inform the user.
                            PrintDebug("Connection failed");
                            mNetwork.Connect(roomName);
                        }
                        break;
                    case NetEventType.Disconnected:
                        {
                            //mConnections.Remove(evt.ConnectionId);
                            //A connection was disconnected
                            //If this was the client then he was disconnected from the server
                            //if it was the server this just means that one of the clients left
                            PrintDebug("Local Connection ID " + evt.ConnectionId + " disconnected");


                            if (peers.ContainsKey(evt.ConnectionId.ToString()))
                            {
                                peers.Remove(evt.ConnectionId.ToString());
                            }

                            if (OnDisconnection != null)
                            {
                                OnDisconnection(evt.ConnectionId);
                            }
                        }
                        break;
                    case NetEventType.ReliableMessageReceived:
                        {
                            if (OnReceivedMessage != null)
                            {
                                OnReceivedMessage(evt);
                            }
                            // Maybe call 
                            // evt.MessageData.Dispose();
                            // ? Idk. Same for Unreliable
                        }
                        break;
                    case NetEventType.UnreliableMessageReceived:
                        {
                            if (OnReceivedMessage != null)
                            {
                                OnReceivedMessage(evt);
                            }
                        }
                        break;
                }
            }

            //finish this update by flushing the messages out if the network wasn't destroyed during update
            if (mNetwork != null)
                mNetwork.Flush();
        }
    }

    ~P2PClient()
    {
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
    }
}