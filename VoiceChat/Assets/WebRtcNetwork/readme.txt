Introduction:

	WebRTC Network is a plugin for Unity WebGL and windows (more coming soon) that allows two 
    games to connect DIRECTLY to each other and send reliable/unreliable messages using WebRTC 
    Datachannels. This makes it well suited for any fast paced real time multiplayer games.

	WebRTC still requires a server to initialize the connection between two users.
	This version will automatically use a test server for this purpose but you can set up and use your own 
    server any time.

	Features:
	- Very simple programming interface. These are all methods you need: Start/StopServer, Connect,
        Disconnect, SendMessage + a Dequeue method to read incomming network events!
	- Send messages reliable or unreliable
	- Contains a complete chat app as an example
    - Platforms supported: WebGL (Firefox, Chrome) + Windows x86 and x64.
    - Unlike many other network libraries for unity WebRtcNetwork fully supports peer-to-peer! You can have multiple
      incoming and outgoing connections at the same time!
    
	Note that Google and Mozilla treat WebRTC still as an experimental technology!
	If you have any questions feel free to contact me.
    
    Visit http://because-why-not.com/webrtc/webrtc-network/ to find the detail documentation
    + API!
    
===============================================================================================================
Setup:
	Make sure you tick "Run in background" in your player settings and add the scene 
    "WebRtcNetwork\example\chatscene" to your build settings.
	
    
Example ChatApp:
    The chat app allows a user to create or join a chat room. After entering a room the users can
    send chat messages to everyone in the room. The user that created the room will act as a server 
    and relay the messages to all connected users.
    
	The examples are stored in the folder WebRtcNetwork\example. The file ChatApp.cs contains
    most of the code and is fully documented. Use this example to learn how to use the library 
    to send any data across the network. 
	
    The example contains two instances of the ChatApp. You can simply connect the two instances
    or start the programm twice and connect them this way.
    
    
	
Important folders, files and classes:
	Assets/WebRtcNetwork/Plugins:
		The folder contains all platform specific components of the library.
        
        WebGL   -   contains the plugin for WebGL. Make sure it is set to active for WebGL!
        win/x64 -   contains the 64-bit version for Windows. Needs to be activated for
                    Unity Editor x86_x64 Windows and Standalone x86_x64 Windows
        win/x86 -   same but for 32 Bit Windows Standalone and Unity Editor
        
        Byn.Common.dll -    contains the IBasicNetwork interface and reusable classes for all platforms (keep at Any Platform in unity)
                            
        WebRtcCSharp.dll and Byn.WebRtcNetwork.Native.dll are for all native platforms
                            (Standalone Windows x86_x64, x64 + Unity Editor x86_x64, x64 but Any Platform should work as well)
		
		
	Assets/WebRtcNetwork/Resources:
		Stores a file "webrtcnetworkplugin.txt". It contains the browser side code of the library. It will be
        automatically injected into the website during setup.
    
    Assets/WebRtcNetwork/scripts/WebRtcNetworkFactory.cs:
        This cs file contains a factory class that allows you to create new Network objects. 
        
    
    Assets/server.zip
        Contains an open source example server used for the signaling process of WebRTC. 
        This is needed to exchange connection information between two peers allowing them
        to connect. The library uses a development server by default but for a release
        version you need to provide your own server! Extract the file and follow the
        instruction of the readme.txt inside.


Do you have questions or problems using the library?
	You can find up to date contact information at http://because-why-not.com/about/ and
    the newest information about this library at http://because-why-not.com/webrtc-network/ !

