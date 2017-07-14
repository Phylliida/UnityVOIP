/* 
 * Copyright (C) 2015 Christoph Kutza
 * 
 * Please refer to the LICENSE file for license information
 */
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Text;
using System;
using Byn.Net;
using System.Collections.Generic;
using Byn.Common;

/// <summary>
/// Contains a complete chat example.
/// It can run on Windows x86/x64 and in browsers. More platforms will be added soon.
/// 
/// The chat app will report during start which system it uses.
/// 
/// The user can enter a room name and click the "Open room" button to start a server and wait for
/// incoming connections or use the "Join room" button to join an already existing room.
/// 
/// 
/// 
/// 
/// As the system implements a server/client style connection all messages will first be sent to the
/// server and the server delivers it to each client. The server side ConnectionId is used to
/// identify a user.
/// 
/// 
/// </summary>
public class ChatApp : MonoBehaviour
{







    /// <summary>
    /// This is a test server. Don't use in production! The server code is in a zip file in WebRtcNetwork
    /// </summary>
    public string uSignalingUrl = "wss://because-why-not.com:12777/chatapp";

    /// <summary>
    /// Mozilla stun server. Used to get trough the firewall and establish direct connections.
    /// Replace this with your own production server as well. 
    /// </summary>
    public string uStunServer = "stun:stun.l.google.com:19302";

    /// <summary>
    /// Set true to use send the WebRTC log + wrapper log output to the unity log.
    /// </summary>
    public bool uLog = false;

    /// <summary>
    /// Debug console to be able to see the unity log on every platform
    /// </summary>
    public bool uDebugConsole = false;

    #region UI
    /// <summary>
    /// Input field used to enter the room name.
    /// </summary>
    public InputField uRoomName;

    /// <summary>
    /// Input field to enter a new message.
    /// </summary>
    public InputField uMessageInput;

    /// <summary>
    /// Output message list to show incoming and sent messages + output messages of the
    /// system itself.
    /// </summary>
    public MessageList uOutput;

    /// <summary>
    /// Join button to connect to a server.
    /// </summary>
    public Button uJoin;

    /// <summary>
    /// Send button.
    /// </summary>
    public Button uSend;

    /// <summary>
    /// Open room button to start a server.
    /// </summary>
    public Button uOpenRoom;

    /// <summary>
    /// Button to leave the room
    /// </summary>
    public Button uLeave;
    #endregion
    /// <summary>
    /// The network interface.
    /// This can be native webrtc or the browser webrtc version.
    /// (Can also be the old or new unity network but this isn't part of this package)
    /// </summary>
    private IBasicNetwork mNetwork = null;

    /// <summary>
    /// True if the user opened an own room allowing incoming connections
    /// </summary>
    private bool mIsServer = false;

    /// <summary>
    /// Keeps track of all current connections
    /// </summary>
    private List<ConnectionId> mConnections = new List<ConnectionId>();


    private const int MAX_CODE_LENGTH = 256;



    /// <summary>
    /// Will setup webrtc and create the network object
    /// </summary>
	private void Start ()
    {
        //shows the console on all platforms. for debugging only
        if(uDebugConsole)
            DebugHelper.ActivateConsole();
        if(uLog)
            SLog.SetLogger(OnLog);

        SLog.LV("Verbose log is active!");
        SLog.LD("Debug mode is active");

        Append("Setting up WebRtcNetworkFactory");
        WebRtcNetworkFactory factory = WebRtcNetworkFactory.Instance;
        if(factory != null)
            Append("WebRtcNetworkFactory created");

    }
    private void OnLog(object msg, string[] tags)
    {
        StringBuilder builder = new StringBuilder();
        TimeSpan time = DateTime.Now - DateTime.Today;
        builder.Append(time);
        builder.Append("[");
        for (int i = 0; i< tags.Length; i++)
        {
            if(i != 0)
                builder.Append(",");
            builder.Append(tags[i]);
        }
        builder.Append("]");
        builder.Append(msg);
        Debug.Log(builder.ToString());
    }

    private void Setup()
    {
        Append("Initializing webrtc network");


        mNetwork = WebRtcNetworkFactory.Instance.CreateDefault(uSignalingUrl, new IceServer[] { new IceServer(uStunServer) });
        if (mNetwork != null)
        {
            Append("WebRTCNetwork created");
        }
        else
        {
            Append("Failed to access webrtc ");
        }
        SetGuiState(false);
    }

    private void Reset()
    {
        Debug.Log("Cleanup!");

        mIsServer = false;
        mConnections = new List<ConnectionId>();
        Cleanup();
        SetGuiState(true);
    }

    /// <summary>
    /// called during reset and destroy
    /// </summary>
    private void Cleanup()
    {
        mNetwork.Dispose();
        mNetwork = null;
    }

    private void OnDestroy()
    {
        if (mNetwork != null)
        {
            Cleanup();
        }
    }

    private void FixedUpdate()
    {
        //check each fixed update if we have got new events
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
                //print to the console for debugging
                Debug.Log(evt);

                //check every message
                switch (evt.Type)
                {
                    case NetEventType.ServerInitialized:
                        {
                            //server initialized message received
                            //this is the reaction to StartServer -> switch GUI mode
                            mIsServer = true;
                            string address = evt.Info;
                            Append("Server started. Address: " + address);
                            uRoomName.text = "" + address;
                        } break;
                    case NetEventType.ServerInitFailed:
                        {
                            //user tried to start the server but it failed
                            //maybe the user is offline or signaling server down?
                            mIsServer = false;
                            Append("Server start failed.");
                            Reset();
                        } break;
                    case NetEventType.ServerClosed:
                        {
                            //server shut down. reaction to "Shutdown" call or
                            //StopServer or the connection broke down
                            mIsServer = false;
                            Append("Server closed. No incoming connections possible until restart.");
                        } break;
                    case NetEventType.NewConnection:
                        {
                            mConnections.Add(evt.ConnectionId);
                            //either user runs a client and connected to a server or the
                            //user runs the server and a new client connected
                            Append("New local connection! ID: " + evt.ConnectionId);

                            //if server -> send announcement to everyone and use the local id as username
                            if(mIsServer)
                            {
                                //user runs a server. announce to everyone the new connection
                                //using the server side connection id as identification
                                string msg = "New user " + evt.ConnectionId + " joined the room.";
                                Append(msg);
                                //SendString(msg);
                            }
                        } break;
                    case NetEventType.ConnectionFailed:
                        {
                            //Outgoing connection failed. Inform the user.
                            Append("Connection failed");
                            Reset();
                        } break;
                    case NetEventType.Disconnected:
                        {
                            mConnections.Remove(evt.ConnectionId);
                            //A connection was disconnected
                            //If this was the client then he was disconnected from the server
                            //if it was the server this just means that one of the clients left
                            Append("Local Connection ID " + evt.ConnectionId + " disconnected");
                            if (mIsServer == false)
                            {
                                Reset();
                            }
                            else
                            {
                                string userLeftMsg = "User " + evt.ConnectionId + " left the room.";

                                //show the server the message
                                Append(userLeftMsg);
                             
                                //other users left? inform them 
                                if (mConnections.Count > 0)
                                {
                                   // SendString(userLeftMsg);
                                }
                            }
                        } break;
                    case NetEventType.ReliableMessageReceived:
                        {
                            HandleIncommingMessage(ref evt);




                        } break;
                    case NetEventType.UnreliableMessageReceived:
                        {
                            HandleIncommingMessage(ref evt);
                        } break;
                }
            }

            //finish this update by flushing the messages out if the network wasn't destroyed during update
            if(mNetwork != null)
                mNetwork.Flush();
        }
    }

    private void HandleIncommingMessage(ref NetworkEvent evt)
    {
        MessageDataBuffer buffer = (MessageDataBuffer)evt.MessageData;

        if (buffer.Buffer[0] == 0)
        {
            

        }

        string msg = Encoding.UTF8.GetString(buffer.Buffer, 0, buffer.ContentLength);

        //if server -> forward the message to everyone else including the sender
        if (mIsServer)
        {
            //we use the server side connection id to identify the client
            string idAndMessage = evt.ConnectionId + ":" + msg;
            SendString(idAndMessage);
            Append(idAndMessage);
        }
        else
        {
            //client received a message from the server -> simply print
            Append(msg);
        }

        //return the buffer so the network can reuse it
        buffer.Dispose();
    }


    /// <summary>
    /// Sends a string as UTF8 byte array to all connections
    /// </summary>
    /// <param name="msg">String containing the message to send</param>
    /// <param name="reliable">false to use unreliable messages / true to use reliable messages</param>
    private void SendString(string msg, bool reliable = true)
    {
        if (mNetwork == null || mConnections.Count == 0)
        {
            Append("No connection. Can't send message.");
        }
        else
        {
            byte[] msgData = Encoding.UTF8.GetBytes(msg);
            foreach (ConnectionId id in mConnections)
            {
                mNetwork.SendData(id, msgData, 0, msgData.Length, reliable);
            }
        }
    }



    #region UI

    private void OnGUI()
    {
        //draws the debug console (or the show button in the corner to open it)
        DebugHelper.DrawConsole();
    }

    /// <summary>
    /// Adds a new message to the message view
    /// </summary>
    /// <param name="text"></param>
    private void Append(string text)
    {
        Debug.Log("chat: " + text);
        uOutput.AddTextEntry(text);
    }


    /// <summary>
    /// Changes the gui depending on if the user is connected
    /// or disconnected
    /// </summary>
    /// <param name="showSetup">true = user is connected. false = user isn't connected</param>
    private void SetGuiState(bool showSetup)
    {
        uJoin.interactable = showSetup;
        uOpenRoom.interactable = showSetup;

        uSend.interactable = !showSetup;
        uLeave.interactable = !showSetup;
        uMessageInput.interactable = !showSetup;
    }

    /// <summary>
    /// Join button pressed. Tries to join a room.
    /// </summary>
    public void JoinRoomButtonPressed()
    {
        Setup();
        mNetwork.Connect(uRoomName.text);
        Append("Connecting to " + uRoomName.text + " ...");
    }
    private void EnsureLength()
    {
        if(uRoomName.text.Length > MAX_CODE_LENGTH)
        {
            uRoomName.text = uRoomName.text.Substring(0, MAX_CODE_LENGTH);
        }
    }

    /// <summary>
    /// Shuts the app down and waits for a restart
    /// </summary>
    public void LeaveButtonPressed()
    {
        Reset();
        SetGuiState(true);
    }
    /// <summary>
    /// Open room button pressed.
    /// 
    /// Opens a room / starts a server
    /// </summary>
    public void OpenRoomButtonPressed()
    {
        Setup();
        EnsureLength();
        mNetwork.StartServer(uRoomName.text);
        
        Debug.Log("StartServer " + uRoomName.text);
    }

    public void InputOnEndEdit()
    {
        if (Input.GetKey(KeyCode.Return))
        {
            SendButtonPressed();
        }
    }

    /// <summary>
    /// This is called if the send button
    /// </summary>
    public void SendButtonPressed()
    {
        //get the message written into the text field
        string msg = uMessageInput.text;


        if (msg.StartsWith("/disconnect"))
        {
            string[] slt = msg.Split(' ');
            if(slt.Length >= 2)
            {
                ConnectionId conId;
                if (short.TryParse(slt[1], out conId.id))
                {
                    mNetwork.Disconnect(conId);
                }
            }
        }

        //if we are the server -> add 0 in front as the server id
        if(mIsServer)
        {
            //the server has the authority thus -> we can print it directly adding the 0 as server id
            msg = "0:" + msg;
            Append(msg);
            SendString(msg);
        }
        else
        {
            //clients just send it directly to the server. the server will decide what to do with it
            SendString(msg);
        }
        uMessageInput.text = "";

        //make sure the text box is in focus again so the user can continue typing without clicking it again
        //select another element first. without this the input field is in focus after return pressed
        uSend.Select();
        uMessageInput.Select();
    }
    #endregion
}
