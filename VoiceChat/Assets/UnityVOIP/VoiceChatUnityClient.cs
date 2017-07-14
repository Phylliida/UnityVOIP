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

        void Start()
        {
            client = new P2PClient(serverURL, roomName);
            recorder = new AudioCapture(8000, 320);
            recorder.OnDataRead += Recorder_OnDataRead;
            client.OnReceivedMessage += Client_OnReceivedMessage;
        }

        bool isRecording = false;

        private void Recorder_OnDataRead(float[] data)
        {
            if (!isRecording)
            {
                return;
            }
            packets.Enqueue(data);
        }
        

        NSpeex.SpeexDecoder speexDec = new NSpeex.SpeexDecoder(NSpeex.BandMode.Narrow);
        NSpeex.SpeexEncoder speexEnc = new NSpeex.SpeexEncoder(NSpeex.BandMode.Narrow);
        private void Client_OnReceivedMessage(Byn.Net.NetworkEvent message)
        {
            int resLen = speexDec.Decode(message.MessageData.Buffer, message.MessageData.Offset, message.MessageData.ContentLength, outBufferShort, 0, false);
            ToFloatArray(outBufferShort, outBufferFloat, resLen);
            player.PlayAudio(outBufferFloat, 0, resLen);
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

        static void ToShortArray(float[] input, short[] output)
        {
            for (int i = 0; i < input.Length; ++i)
            {
                output[i] = (short)Mathf.Clamp((int)(input[i] * 32767.0f), short.MinValue, short.MaxValue);
            }
        }

        static void ToFloatArray(short[] input, float[] output, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                output[i] = input[i] / (float)short.MaxValue;
            }
        }

        private void Update()
        {
            isRecording = Input.GetKey(KeyCode.P);
            client.UpdateClient();
            if (client.peers.Count > 0)
            {
                while (packets.Count > 0)
                {
                    float[]  curPacket = packets.Dequeue();
                    ToShortArray(curPacket, outBufferShort);
                    int resLen = speexEnc.Encode(outBufferShort, 0, curPacket.Length, outBuffer, 0, outBuffer.Length);
                    client.SendMessageToAll(outBuffer, 0, resLen, true);
                }
            }
        }
    } 
}