using UnityEngine;
using CSCore;
using CSCore.SoundIn;
using CSCore.Streams;
using System;
using CSCore.SoundOut;
using CSCore.CoreAudioAPI;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Byn.Net;

namespace UnityVOIP
{
    public class AudioCapture : IDisposable
    {
        public WasapiCapture capture;
        public IWaveSource actualSource;
        PureDataSource dataSource;

        public enum ConverterQuality
        {
            SRC_SINC_FASTEST = 2,
            SRC_SINC_MEDIUM_QUALITY = 1,
            SRC_SINC_BEST_QUALITY = 0
        };

        [DllImport("samplerate", EntryPoint = "src_simple_plain")]
        public static extern int src_simple_plain(float[] data_in, float[] data_out, int input_frames, int output_frames, float src_ratio, ConverterQuality converter_type, int channels);

        private int sampleRate;
        public int SampleRate
        {
            get
            {
                return sampleRate;
            }
            private set
            {

            }
        }
        private int sampleSize;
        public int SampleSize
        {
            get
            {
                return sampleSize;
            }
            private set
            {

            }
        }
        IWaveSource finalSource;
        public AudioCapture(int sampleRate, int sampleSize)
        {
            this.sampleRate = sampleRate;
            this.sampleSize = sampleSize;

            if (sampleSize <= 0)
            {
                throw new ArgumentException("Sample size must be > 0, instead it is " + sampleSize);
            }

            resSamples = new float[this.sampleSize];
            var ayy = new MMDeviceEnumerator();
            // This uses the wasapi api to get any sound data played by the computer
            capture = new WasapiCapture(false, AudioClientShareMode.Shared, 100);
            capture.Device = ayy.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            capture.Initialize();
            capture.DataAvailable += Capture_DataAvailable;

            IWaveSource source = new SoundInSource(capture);


            dataSource = new PureDataSource(new WaveFormat(sampleRate, 8, 1), source.ToSampleSource());
            dataSource.OnDataRead += DataSource_OnDataRead;

            finalSource = dataSource.ToWaveSource();

            capture.Start();
        }

        public delegate void OnDataReadCallback(float[] data, int offset, int len);

        public event OnDataReadCallback OnDataRead;


        float[] remainingSamples = new float[100000];
        int numRemainingSamples = 0;
        float[] resSamples;

        public void Poll()
        {
            if (numRemainingSamples >= SampleSize)
            {
                //Buffer.BlockCopy(remainingSamples, 0, resSamples, 0, SampleSize * sizeof(float));

                int fadeSize = 10;
                fadeSize = Mathf.Min(fadeSize, SampleSize / 2 - 1);
                if (fadeSize > 1)
                {
                    int startInd = 0 + SampleSize - fadeSize;
                    for (int i = 0; i < fadeSize; i++)
                    {
                        float fade = 1 - (i / (float)(fadeSize - 1));
                        remainingSamples[startInd + i] *= fade * fade;
                        float startFade = 1 - fade;
                        remainingSamples[i] *= startFade * startFade;
                    }
                }

                if (OnDataRead != null)
                {
                    OnDataRead(remainingSamples, 0, SampleSize);
                }
                int numLeft = numRemainingSamples - SampleSize;
                lock (cleanupLock)
                {
                    if (numLeft > 0)
                    {
                        Buffer.BlockCopy(remainingSamples, SampleSize * sizeof(float), remainingSamples, 0, numLeft * sizeof(float));
                    }
                    numRemainingSamples -= SampleSize;
                }
            }
        }

        private void DataSource_OnDataRead(float[] data, int dataOffset, int len)
        {
            if (len == 0)
            {
                return;
            }
            lock (cleanupLock)
            {
                if (!cleanedUp)
                {
                    if (sampleSize > 0)
                    {
                        if (len + numRemainingSamples > remainingSamples.Length)
                        {
                            len = remainingSamples.Length - numRemainingSamples;
                        }
                        if (len == 0)
                        {
                            return;
                        }


                        int fadeSize = 7;
                        fadeSize = Mathf.Min(fadeSize, len / 2 - 1);
                        if (fadeSize > 1)
                        {
                            int startInd = dataOffset + len - fadeSize;
                            for (int i = 0; i < fadeSize; i++)
                            {
                                float fade = 1 - (i / (float)(fadeSize - 1));
                                data[startInd + i] *= fade * fade;
                                float startFade = 1 - fade;
                                data[dataOffset + i] *= startFade * startFade;
                            }
                        }


                        Buffer.BlockCopy(data, dataOffset*sizeof(float), remainingSamples, numRemainingSamples * sizeof(float), len * sizeof(float));
                        numRemainingSamples += len;
                    }
                }
            }
            /*
            return;

                    //server.UpdateServer();
                    //client.UpdateClient();
                    if (data != null && len > 0)
                    {
                        Debug.Log("writing");
                        outSource.Write(data, dataOffset, len);
                    }
                }
            }
            return;

            while (true)
            {
                byte[] packet = codec.GetPacket();
                if (packet == null)
                {
                    break;
                }
                lock (cleanupLock)
                {
                    if (!cleanedUp)
                    {
                        //server.SendMessageToAll(packet, false);
                        server.SendMessageToAll(packet, 0, packet.Length, true);
                    }
                }
            }
            */
        }

        bool wantsToCleanUp = false;
        bool isEditor;


        private void Capture_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            try
            {
                if (wantsToCleanUp || capture == null)
                {
                    return;
                }

               
                if (!cleanedUp)
                {
                    byte[] curData = new byte[e.ByteCount];
                    finalSource.Read(curData, 0, e.ByteCount);
                }
            }

            catch (Exception ex)
            {
                Debug.Log(ex.Message);
            }
        }

        [HideInInspector]
        public bool cleanedUp = false;

        object cleanupLock = new object();

        void Cleanup()
        {
            wantsToCleanUp = true;
            lock (cleanupLock)
            {
                if (!cleanedUp)
                {
                    cleanedUp = true;
                    if (capture != null)
                    {
                        while (capture.RecordingState == RecordingState.Recording)
                        {
                            capture.Stop();
                        }
                        capture.Dispose();
                        capture = null;
                    }
                }
            }
        }


        private void OnDestroy()
        {
            Cleanup();
        }


        void OnApplicationQuit()
        {
            Cleanup();
        }

        public void Dispose()
        {
            Cleanup();
        }

        ~AudioCapture()
        {
            Dispose();
        }

        public class PureDataSource : ISampleSource
        {
            public long Length
            {
                get
                {
                    return 0;
                }
            }

            public long Position
            {
                get
                {
                    return 0;
                }

                set
                {
                    throw new NotImplementedException();
                }
            }
            private WaveFormat _WaveFormat;
            public WaveFormat WaveFormat
            {
                get
                {
                    return _WaveFormat;
                }
            }

            private int _Patch = 0;
            public int Patch
            {
                get { return _Patch; }
                //set { _Patch = value; }
            }

            public bool CanSeek
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public delegate void DataReadCallback(float[] data, int dataOffset, int len);

            public event DataReadCallback OnDataRead;

            public ISampleSource source;

            public PureDataSource(WaveFormat waveFormat, ISampleSource source)
            {
                _WaveFormat = waveFormat;
                this.source = source;
            }

            public ConverterQuality quality;

            float[] tempBuffer1 = new float[80000 * 20];
            float[] tempBuffer2 = new float[80000 * 20];
            float lastThing = 0;

            public int Read(float[] buffer, int offset, int count)
            {
                int numRead = ActualRead(buffer, offset, count);
                //int numRead = source.Read(buffer, offset, count);

                

                if (OnDataRead != null && numRead > 0)
                {
                    if (source.WaveFormat.Channels == 2)
                    {
                        for (int i = 0; i < numRead / 2; i++)
                        {
                            tempBuffer1[i] = (buffer[offset + i * 2] + buffer[offset + i * 2 + 1]) / 2.0f;
                        }
                        OnDataRead(tempBuffer1, 0, numRead/2);
                    }
                    else
                    {
                        OnDataRead(buffer, offset, numRead);
                    }
                }
                return count;
                return numRead;
            }

            private int ActualRead(float[] buffer, int offset, int count)
            {
                int sourceSampleRate = source.WaveFormat.SampleRate;
                int mySampleRate = WaveFormat.SampleRate;

                int numTheirSamples = (int)Mathf.Floor((count * (float)sourceSampleRate / mySampleRate));
                if (source == null)
                {
                    return 0;
                }
                int res = 100;
                try
                {
                    res = source.Read(tempBuffer1, 0, numTheirSamples);
                    if (res == 0)
                    {
                        if (count == 0)
                        {
                            return 0;
                        }
                        if (count == 1)
                        {
                            buffer[offset] = lastThing;
                            return 1;
                        }
                        if (count >= 2)
                        {
                            buffer[offset] = lastThing * 0.8f;
                            buffer[offset + 1] = lastThing * 0.6f;
                            return 2;
                        }
                    }
                    int numOurSamples = (int)Mathf.Round((res * (float)mySampleRate / sourceSampleRate));
                    res = src_simple_plain(tempBuffer1, tempBuffer2, res, numOurSamples, (float)mySampleRate / sourceSampleRate, ConverterQuality.SRC_SINC_FASTEST, source.WaveFormat.Channels);
                    if (res > count)
                    {
                        res = count;
                    }
                    Buffer.BlockCopy(tempBuffer2, 0, buffer, offset*sizeof(float), res * sizeof(float));
                }
                catch (Exception e)
                {
                    Debug.Log("failed read: " + e.Message);
                }

                return Mathf.Max(res, 2);
            }

            public void Dispose()
            {

            }
        }

        public ConverterQuality quality = ConverterQuality.SRC_SINC_BEST_QUALITY;
    }
}
