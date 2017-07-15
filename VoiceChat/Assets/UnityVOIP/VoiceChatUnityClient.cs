using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using Byn.Net;

namespace UnityVOIP
{
    public class VoiceChatUnityClient : MonoBehaviour
    {
        public string serverURL = "wss://nameless-scrubland-88927.herokuapp.com";
        public string roomName = "voicechattest";

        public AudioCapture recorder;
        public WritableAudioPlayer player;
        P2PClient client;
        Queue<float[]> packets = new Queue<float[]>(16);

        System.Random random;
        void Start()
        {
            client = new P2PClient(serverURL, roomName);
            recorder = new AudioCapture(8000, 320);
            recorder.OnDataRead += Recorder_OnDataRead;
            client.OnReceivedMessage += Client_OnReceivedMessage;

            random = new System.Random();
            myId = (int)random.Next();

            byte[] bytes = BitConverter.GetBytes(myId);
            id1 = bytes[0];
            id2 = bytes[1];
            id3 = bytes[2];
            id4 = bytes[3];
        }

        bool isRecording = false;

        byte id1, id2, id3, id4;
        int myId;

        byte isSpeechId = 10;

        private void Recorder_OnDataRead(float[] data, int offset, int len)
        {
            if (!isRecording)
            {
                //return;
            }
            ToShortArray(data, outBufferShort, offset, len);
            int resLen = speexEnc.Encode(outBufferShort, 0, len, outBuffer, 5, 5*320);
            outBuffer[0] = isSpeechId;
            outBuffer[1] = id1;
            outBuffer[2] = id2;
            outBuffer[3] = id3;
            outBuffer[4] = id4;
            client.SendMessageToAll(outBuffer, 0, resLen, true);

        }
        

        NSpeex.SpeexDecoder speexDec = new NSpeex.SpeexDecoder(NSpeex.BandMode.Narrow);
        NSpeex.SpeexEncoder speexEnc = new NSpeex.SpeexEncoder(NSpeex.BandMode.Narrow);
        private void Client_OnReceivedMessage(Byn.Net.NetworkEvent message)
        {
            if (message.MessageData.ContentLength < 5)
            {
                return;
            }
            int offset = message.MessageData.Offset;
            byte[] messageBuffer = message.MessageData.Buffer;

            int messageId = messageBuffer[offset];
            int pid = BitConverter.ToInt32(messageBuffer, offset + 1);

            offset += 5;

            if (messageId == isSpeechId)
            {
                int resLen = speexDec.Decode(message.MessageData.Buffer, offset, message.MessageData.ContentLength, outBufferShort, 0, false);
                ToFloatArray(outBufferShort, outBufferFloat, resLen);
                int bean = message.ConnectionId.id;
                player.PlayAudio(outBufferFloat, 0, resLen, pid);
            }
        }

        public void OnDestroy()
        {
            recorder.Dispose();
        }

        public void OnApplicationQuit()
        {
            recorder.Dispose();
        }

        byte[] outBuffer = new byte[100000];

        short[] outBufferShort = new short[100000];
        float[] outBufferFloat = new float[100000];

        static void ToShortArray(float[] input, short[] output, int offset, int len)
        {
            for (int i = 0; i < len; ++i)
            {
                output[i] = (short)Mathf.Clamp((int)(input[i+ offset] * 32767.0f), short.MinValue, short.MaxValue);
            }
        }

        static void ToFloatArray(short[] input, float[] output, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                output[i] = input[i] / (float)short.MaxValue;
            }
        }

        void FixedUpdate()
        {
            recorder.Poll();
            client.UpdateClient();
        }

        private void Update()
        {
            isRecording = Input.GetKey(KeyCode.P);
            if (client.peers.Count > 0)
            {
                //while(packets.Count > 2)
                {
                    //packets.Dequeue();
                }
            }
        }
    } 
}