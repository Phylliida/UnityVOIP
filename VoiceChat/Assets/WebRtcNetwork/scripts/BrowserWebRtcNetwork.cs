/* 
 * Copyright (C) 2015 Christoph Kutza
 * 
 * Please refer to the LICENSE file for license information
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Byn.Net
{
    /// <summary>
    /// Uses an underlaying java script library to give network access in browsers.
    /// 
    /// Use WebRtcNetwork.IsAvailable() first to check if it can run. If the java script part of the library
    /// is included + the browser supports WebRtc it should return true. If the java script part of the
    /// library is not included you can inject it at runtime by using 
    /// WebRtcNetwork.InjectJsCode(). It is recommended to include the js files though.
    /// 
    /// To allow incoming connections use StartServer() or StartServer("my room name")
    /// To connect others use Connect("room name");
    /// To send messages use SendData.
    /// You will need to handle incoming events by polling the Dequeue method.
    /// </summary>
    public class BrowserWebRtcNetwork : IWebRtcNetwork
    {

        //these are functions implemented in the java script plugin file WebRtcNetwork.jslib
        #region CAPI imports
        [DllImport("__Internal")]
        private static extern bool UnityWebRtcNetworkIsAvailable();

        [DllImport("__Internal")]
        private static extern int UnityWebRtcNetworkCreate(string lConfiguration);

        [DllImport("__Internal")]
        private static extern void UnityWebRtcNetworkRelease(int lIndex);

        [DllImport("__Internal")]
        private static extern int UnityWebRtcNetworkConnect(int lIndex, string lRoom);

        [DllImport("__Internal")]
        private static extern void UnityWebRtcNetworkStartServer(int lIndex, string lRoom);
        [DllImport("__Internal")]
        private static extern void UnityWebRtcNetworkStopServer(int lIndex);
        
        [DllImport("__Internal")]
        private static extern void UnityWebRtcNetworkDisconnect(int lIndex, int lConnectionId);

        [DllImport("__Internal")]
        private static extern void UnityWebRtcNetworkShutdown(int lIndex);
        [DllImport("__Internal")]
        private static extern void UnityWebRtcNetworkUpdate(int lIndex);
        [DllImport("__Internal")]
        private static extern void UnityWebRtcNetworkFlush(int lIndex);

        [DllImport("__Internal")]
        private static extern void UnityWebRtcNetworkSendData(int lIndex, int lConnectionId, byte[] lUint8ArrayDataPtr, int lUint8ArrayDataOffset, int lUint8ArrayDataLength, bool lReliable);

        [DllImport("__Internal")]
        private static extern int UnityWebRtcNetworkPeekEventDataLength(int lIndex);

        [DllImport("__Internal")]
        private static extern bool UnityWebRtcNetworkDequeue(int lIndex,
            int[] lTypeIntArrayPtr,
            int[] lConidIntArrayPtr,
            byte[] lUint8ArrayDataPtr, int lUint8ArrayDataOffset, int lUint8ArrayDataLength,
            int[] lDataLenIntArray);
        [DllImport("__Internal")]
        private static extern bool UnityWebRtcNetworkPeek(int lIndex,
            int[] lTypeIntArrayPtr,
            int[] lConidIntArrayPtr,
            byte[] lUint8ArrayDataPtr, int lUint8ArrayDataOffset, int lUint8ArrayDataLength,
            int[] lDataLenIntArray);
        #endregion

        private static bool sInjectionTried = false;

        /// <summary>
        /// This injects the library using ExternalEval. Browsers seem to load some libraries asynchronously though.
        /// This means that directly after InjectJsCode some things aren't available yet. 
        /// So starting a server/connecting won't work directly after this call yet. Add at least 1-2 seconds waiting time
        /// for the browser to download the libraries or better -> include everything needed into the websites header
        /// so this call isn't needed at all!
        /// </summary>
        static public void InjectJsCode()
        {

            //use sInjectionTried to block multiple calls.
            if (Application.platform == RuntimePlatform.WebGLPlayer && sInjectionTried == false)
            {
                sInjectionTried = true;
                Debug.Log("injecting webrtcnetworkplugin");
                TextAsset txt = Resources.Load<TextAsset>("webrtcnetworkplugin");
                if(txt == null)
                {
                    Debug.LogError("Failed to find webrtcnetworkplugin.txt in Resource folder. Can't inject the JS plugin!");
                    return;
                }
                Application.ExternalEval(txt.text);
            }
        }
        /// <summary>
        /// Will return true if the environment supports the WebRTCNetwork plugin
        /// (needs to run in Chrome or Firefox + the javascript file needs to be loaded in the html page!)
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool IsAvailable()
        {
            try
            {
                return UnityWebRtcNetworkIsAvailable();
            }catch(EntryPointNotFoundException)
            {
                //not available at all
                return false;
            }
        }


        protected int mReference = -1;

        /// <summary>
        /// Returns true if the server is running or the client is connected.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                if (mIsServer)
                    return true;

                if (mConnections.Count > 0)
                    return true;
                return false;
            }
        }

        private bool mIsServer = false;
        /// <summary>
        /// True if the server is running allowing incoming connections
        /// </summary>
        public bool IsServer
        {
            get { return mIsServer; }
        }



        

        private List<ConnectionId> mConnections = new List<ConnectionId>();



        private int[] mTypeidBuffer = new int[1];
        private int[] mConidBuffer = new int[1];
        private int[] mDataWrittenLenBuffer = new int[1];

        private Queue<NetworkEvent> mEvents = new Queue<NetworkEvent>();
        

        /// <summary>
        /// Creates a new network by using a JSON configuration string. This is used to configure the server connection for the signaling channel
        /// and to define webrtc specific configurations such as stun server used to connect through firewalls.
        /// 
        /// might look like this:
        /// string config = "{ 'signaling' :  { 'class': 'WebsocketNetwork', 'param' : 'wss://localhost:12777'}, 'iceServers':['stun:stun.l.google.com:19302']}";
        /// 
        /// Make sure all json " signs are escaped. The JSON object will be parsed in java script and then given to the library. So their content is 
        /// dependent on the underlaying library. Check out the webrtcnetwork.js to understand the details.
        /// </summary>
        /// <param name="config"></param>
        public BrowserWebRtcNetwork(string config)
        {
            Debug.Log(config);
            mReference = UnityWebRtcNetworkCreate(config);
        }
        /// <summary>
        /// For subclasses that provide their own init process
        /// </summary>
        protected BrowserWebRtcNetwork()
        {

        }

        /// <summary>
        /// Destructor to make sure everything gets disposed. Sadly, WebGL doesn't seem to call this ever.
        /// </summary>
        ~BrowserWebRtcNetwork()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the underlaying java script library. If you have long running systems that don't reuse instances make sure
        /// you always call dispose as unity doesn't seem to call destructors reliably. You might fill up your java script
        /// memory with lots of unused instances.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            //just to follow the pretty dispose pattern
            if (disposing)
            {
                if (mReference != -1)
                {
                    UnityWebRtcNetworkRelease(mReference);
                    mReference = -1;
                }
            }
            else
            {
                if (mReference != -1)
                    UnityWebRtcNetworkRelease(mReference);
            }
        }
        
        /// <summary>
        /// Starts a server using a random number as address/name.
        /// 
        /// Read the ServerInitialized events Info property to get the address name.
        /// </summary>
        public void StartServer()
        {
            StartServer("" + UnityEngine.Random.Range(0, 16777216));
        }

        /// <summary>
        /// Allows to listen to incoming connections using a given name/address.
        /// 
        /// This is in addition to the definition of the IBaseNetwork interface which is
        /// shared with other network systems enforcing the use of ip:port as address, thus
        /// can't allow custom addresses.
        /// </summary>
        /// <param name="name">Name/Address can be any kind of string. There might be restrictions though depending
        /// on the underlaying signaling channel.
        /// An invalid name will result in an InitFailed event being return in Dequeue.</param>
        public void StartServer(string name)
        {
            if (this.mIsServer == true)
            {
                UnityEngine.Debug.LogError("Already in server mode.");
                return;
            }
            UnityWebRtcNetworkStartServer(mReference, name);
        }

        public void StopServer()
        {
            UnityWebRtcNetworkStopServer(mReference);
        }


        /// <summary>
        /// Connects to the given name or address.
        /// </summary>
        /// <param name="name"> The address identifying the server  </param>
        /// <returns>
        /// The connection id. (WebRTCNetwork doesn't allow multiple connections yet! So you can ignore this for now)
        /// </returns>
        public ConnectionId Connect(string name)
        {
            //until fully supported -> block connecting to others while running a server
            if (this.mIsServer == true)
            {
                UnityEngine.Debug.LogError("Can't create outgoing connections while in server mode!");
                return ConnectionId.INVALID;
            }

            ConnectionId id = new ConnectionId();
            id.id = (short)UnityWebRtcNetworkConnect(mReference, name);
            return id;
        }


        /// <summary>
        /// Retrieves an event from the js library, handles it internally and then adds it to a queue for delivery to the user.
        /// </summary>
        /// <param name="evt"> The new network event or an empty struct if none is found.</param>
        /// <returns>True if event found, false if no events queued.</returns>
        private bool DequeueInternal(out NetworkEvent evt)
        {
            int length = UnityWebRtcNetworkPeekEventDataLength(mReference);
            if(length == -1) //-1 == no event available
            {
                evt = new NetworkEvent();
                return false;
            }
            else
            {
                ByteArrayBuffer buf = ByteArrayBuffer.Get(length);
                bool eventFound = UnityWebRtcNetworkDequeue(mReference, mTypeidBuffer, mConidBuffer, buf.array, 0, buf.array.Length, mDataWrittenLenBuffer);
                //set the write correctly
                buf.PositionWriteRelative = mDataWrittenLenBuffer[0];

                NetEventType type = (NetEventType)mTypeidBuffer[0];
                ConnectionId id;
                id.id = (short)mConidBuffer[0];
                object data = null;

                if (buf.PositionWriteRelative == 0 || buf.PositionWriteRelative == -1) //no data
                {
                    data = null;
                    //was an empty buffer -> release it and 
                    buf.Dispose();
                }
                else if (type == NetEventType.ReliableMessageReceived || type == NetEventType.UnreliableMessageReceived)
                {
                    //store the data for the user to use
                    data = buf;
                }
                else
                {
                    //non data message with data attached -> can only be a string
                    string stringData = Encoding.ASCII.GetString(buf.array, 0, buf.PositionWriteRelative);
                    data = stringData;
                    buf.Dispose();

                }


                evt = new NetworkEvent(type, id, data);
                UnityEngine.Debug.Log("event" + type + " received");
                HandleEventInternally(ref evt);
                return eventFound;
            }

        }

        /// <summary>
        /// Handles events internally. Needed to change the internal states: Server flag and connection id list.
        /// 
        /// Would be better to remove that in the future from the main library and treat it separately. 
        /// </summary>
        /// <param name="evt"> event to handle </param>
        private void HandleEventInternally(ref NetworkEvent evt)
        {
            if(evt.Type == NetEventType.NewConnection)
            {
                mConnections.Add(evt.ConnectionId);
            }else if(evt.Type == NetEventType.Disconnected)
            {
                mConnections.Remove(evt.ConnectionId);
            }else if(evt.Type == NetEventType.ServerInitialized)
            {
                mIsServer = true;
            }
            else if (evt.Type == NetEventType.ServerClosed || evt.Type == NetEventType.ServerInitFailed)
            {
                mIsServer = false;
            }
        }

        /// <summary>
        /// Sends a byte array
        /// </summary>
        /// <param name="conId">Connection id the message should be delivered to.</param>
        /// <param name="data">Content/Buffer that contains the content</param>
        /// <param name="offset">Start index of the content in data</param>
        /// <param name="length">Length of the content in data</param>
        /// <param name="reliable">True to use the ordered, reliable transfer, false for unordered and unreliable</param>
        public void SendData(ConnectionId conId, byte[] data, int offset, int length, bool reliable)
        {
            UnityWebRtcNetworkSendData(mReference, conId.id, data, offset, length, reliable);
        }

        /// <summary>
        /// Shuts webrtc down. All connection will be disconnected + if the server is started it will be stopped.
        /// 
        /// The instance itself isn't released yet! Use Dispose to destroy the network entirely.
        /// </summary>
        public void Shutdown()
        {
            UnityWebRtcNetworkShutdown(mReference);
        }

        /// <summary>
        /// Dequeues a new event
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        public bool Dequeue(out NetworkEvent evt)
        {
            evt = new NetworkEvent();
            if (mEvents.Count == 0)
                return false;

            evt = mEvents.Dequeue();
            return true;
        }

        public bool Peek(out NetworkEvent evt)
        {
            evt = new NetworkEvent();
            if (mEvents.Count == 0)
                return false;

            evt = mEvents.Peek();
            return true;
        }

        /// <summary>
        /// Needs to be called to read data from the underlaying network and update this class.
        /// 
        /// Use Dequeue to get the events it read.
        /// </summary>
        public virtual void Update()
        {
            UnityWebRtcNetworkUpdate(mReference);
            
            NetworkEvent ev = new NetworkEvent();

            //DequeueInternal will read the message from js, change the state of this object
            //e.g. if a server is successfully opened it will set mIsServer to true
            while(DequeueInternal(out ev))
            {
                //add it for delivery to the user
                mEvents.Enqueue(ev);
            }
        }

        /// <summary>
        /// Flushes messages. Not needed in WebRtcNetwork but use it at the end of a frame 
        /// if you want to be able to replace WebRtcNetwork with other implementations
        /// </summary>
        public void Flush()
        {
            UnityWebRtcNetworkFlush(mReference);
        }

        /// <summary>
        /// Disconnects the given connection id.
        /// </summary>
        /// <param name="id">Id to disconnect</param>
        public void Disconnect(ConnectionId id)
        {
            UnityWebRtcNetworkDisconnect(mReference, id.id);
        }

    }
}
