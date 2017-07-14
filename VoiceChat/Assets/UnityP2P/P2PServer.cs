using Byn.Net;
using System;
using System.Collections.Generic;
using UnityEngine;


public class P2PServer : IDisposable
{
    public Dictionary<string, ConnectionId> peers;
    IBasicNetwork mNetwork = null;
    bool mIsServer = false;
    string roomName;

    DateTime lastTimeGotAnything;

    public P2PServer(string signalingServer, string roomName)
    {
        this.roomName = roomName;
        lastTimeGotAnything = DateTime.Now;
        peers = new Dictionary<string, ConnectionId>();
        //mNetwork = WebRtcNetworkFactory.Instance.CreateDefault("wss://nameless-scrubland-88927.herokuapp.com", new IceServer[] { new IceServer("stun:stun.l.google.com:19302") });
        mNetwork = WebRtcNetworkFactory.Instance.CreateDefault(signalingServer, new IceServer[] { new IceServer("stun:stun.l.google.com:19302") });
        if (mNetwork == null)
        {
            PrintDebug("failed to setup network");
            return;
        }

        mNetwork.StartServer(roomName);
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
        lock (peers)
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
    }

    public void SendMessageToAll(byte[] data, bool isReliable)
    {
        SendMessageToAll(data, 0, data.Length, isReliable);
    }

    public void SendMessageToAll(byte[] data, int dataOffset, int dataLen, bool isReliable)
    {
        if (mNetwork != null)
        {
            lock (peers)
            {
                foreach (KeyValuePair<string, ConnectionId> peer in peers)
                {
                    mNetwork.SendData(peer.Value, data, dataOffset, dataLen, isReliable);
                }
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
            if (mIsServer)
            {
                mNetwork.StopServer();
                mIsServer = false;
            }
            mNetwork.Dispose();
            mNetwork = null;
        }
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    void PrintDebug(string message)
    {
        Debug.Log(message);
    }

    private void OnApplicationQuit()
    {
        Cleanup();
    }

    long frameCount = 0;
    // Update is called once per frame
    public void UpdateServer()
    {
        frameCount++;

        if (!mIsServer && frameCount % 50000 == 0)
        {
            mNetwork.StartServer(roomName);
        }

        // It has been 40 seconds since got any messages, reset server before it decides to die
        if ((DateTime.Now - lastTimeGotAnything).TotalSeconds > 40)
        {
            PrintDebug("Server timeout after 40 seconds, restarting server");
            if (mIsServer)
            {
                mNetwork.StopServer();
            }
            mIsServer = false;
            mNetwork.StartServer(roomName);
            lastTimeGotAnything = DateTime.Now;
        }

        HandleNetwork();
    }



    private void HandleNetwork()
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
                lastTimeGotAnything = DateTime.Now;
                //check every message
                switch (evt.Type)
                {
                    case NetEventType.ServerInitialized:
                        {
                            //server initialized message received
                            mIsServer = true;
                            string address = evt.Info;
                            PrintDebug("Server started. Address: " + address);
                        }
                        break;
                    case NetEventType.ServerInitFailed:
                        {
                            //user tried to start the server but it failed
                            //maybe the user is offline or signaling server down?
                            PrintDebug("Server start failed.");
                            mIsServer = false;
                        }
                        break;
                    case NetEventType.ServerClosed:
                        {
                            PrintDebug("Server closed");
                            mNetwork.StartServer(roomName);
                        }
                        break;
                    case NetEventType.NewConnection:
                        {
                            //user runs a server. announce to everyone the new connection
                            //using the server side connection id as identification
                            string msg = "New user " + evt.ConnectionId + " joined the room.";
                            PrintDebug(msg);

                            lock (peers)
                            {
                                peers[evt.ConnectionId.ToString()] = evt.ConnectionId;
                            }

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
                        }
                        break;
                    case NetEventType.Disconnected:
                        {
                            //mConnections.Remove(evt.ConnectionId);
                            //A connection was disconnected
                            //If this was the client then he was disconnected from the server
                            //if it was the server this just means that one of the clients left
                            PrintDebug("Local Connection ID " + evt.ConnectionId + " disconnected");
                            lock (peers)
                            {
                                if (peers.ContainsKey(evt.ConnectionId.ToString()))
                                {
                                    peers.Remove(evt.ConnectionId.ToString());
                                }
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


    ~P2PServer()
    {
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
    }
}