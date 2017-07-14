using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityVOIP
{
    public class WritableAudioPlayer : MonoBehaviour
    {
        WasapiOut output;
        WritablePureDataSource outSource;

        public int inputSampleRate = 8000;

        bool cleanedUp = false;
        object cleanupLock = new object();

        // Use this for initialization
        void Start()
        {
            var ayy = new MMDeviceEnumerator();
            output = new WasapiOut(false, AudioClientShareMode.Shared, 100);
            output.Device = ayy.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            outSource = new WritablePureDataSource(new WaveFormat(inputSampleRate, 8, 1), new WaveFormat(output.Device.DeviceFormat.SampleRate, 8, output.Device.DeviceFormat.Channels));

            output.Initialize(outSource.ToWaveSource());

            output.Play();
        }

        public float TimeUntilDone
        {
            get
            {
                return outSource.numUnprocessed / (float)outSource.waveFormatIn.SampleRate;
            }
            private set
            {

            }
        }

        public void PlayAudio(float[] audio, int offset, int len)
        {
            outSource.Write(audio, offset, len);
        }
        public void PlayAudio(float[] audio)
        {
            PlayAudio(audio, 0, audio.Length);
        }

        void Cleanup()
        {
            lock (cleanupLock)
            {
                if (!cleanedUp)
                {
                    cleanedUp = true;
                    if (output != null)
                    {
                        if (output.PlaybackState == PlaybackState.Paused)
                        {
                            output.Stop();
                        }
                        while (output.PlaybackState == PlaybackState.Playing)
                        {
                            output.Stop();
                        }
                        output.Dispose();
                    }
                }
            }
        }

        void OnDestroy()
        {
            Cleanup();
        }
        void OnApplicationQuit()
        {
            Cleanup();
        }
    }


    public enum ConverterQuality
    {
        SRC_SINC_FASTEST = 2,
        SRC_SINC_MEDIUM_QUALITY = 1,
        SRC_SINC_BEST_QUALITY = 0
    };

    public class WritablePureDataSource : ISampleSource
    {

        [DllImport("samplerate", EntryPoint = "src_simple_plain")]
        public static extern int src_simple_plain(float[] data_in, float[] data_out, int input_frames, int output_frames, float src_ratio, ConverterQuality converter_type, int channels);


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

        public WaveFormat waveFormatIn;
        public WaveFormat waveFormatOut;

        public WritablePureDataSource(WaveFormat waveFormatIn, WaveFormat waveFormatOut)
        {
            this.waveFormatIn = waveFormatIn;
            _WaveFormat = waveFormatOut;
            this.waveFormatOut = waveFormatOut;
        }






        float[] tempBuffer1 = new float[80000 * 20];
        float[] tempBuffer2 = new float[80000 * 20];
        float[] tempBuffer3 = new float[80000 * 20];

        float[] unprocessedAudio = new float[80000 * 20];
        public int numUnprocessed = 0;

        private void AddToUnprocessedData(float[] data, int offset, int numBytes, int inChannels)
        {
            if (inChannels == 1 && waveFormatOut.Channels == 2)
            {
                int pos = 0;
                for (int i = 0; i < numBytes && pos < tempBuffer3.Length - 2; i++)
                {
                    tempBuffer3[pos++] = data[i + offset];
                    tempBuffer3[pos++] = data[i + offset];
                }
                AddToUnprocessedData(tempBuffer3, 0, numBytes * 2, 2);
                return;
            }
            if (numUnprocessed + numBytes > unprocessedAudio.Length)
            {
                numBytes = unprocessedAudio.Length - numUnprocessed;
            }
            if (numBytes == 0)
            {
                return;
            }

            Buffer.BlockCopy(data, offset * sizeof(float), unprocessedAudio, numUnprocessed * sizeof(float), numBytes * sizeof(float));

            numUnprocessed += numBytes;
        }

        public void Write(float[] buffer, int offset, int count)
        {
            lock (unprocessedAudio)
            {
                AddToUnprocessedData(buffer, offset, count, waveFormatIn.Channels);
            }
        }

        public int ReadUnprocessedData(float[] buffer, int offset, int count)
        {
            lock (unprocessedAudio)
            {
                int countUsing = Mathf.Min(count, numUnprocessed);

                if (countUsing <= 0)
                {
                    if (count > 0)
                    {
                        Array.Clear(buffer, offset, count);
                        return count;
                    }
                    else
                    {
                        return 0;
                    }
                }
                Buffer.BlockCopy(unprocessedAudio, 0, buffer, offset * sizeof(float), countUsing * sizeof(float)); // size is in bytes

                int numLeft = numUnprocessed - countUsing;
                if (numUnprocessed > 0)
                {
                    Buffer.BlockCopy(unprocessedAudio, countUsing * sizeof(float), unprocessedAudio, 0, numLeft * sizeof(float));
                }
                numUnprocessed = numLeft;


                return countUsing;
            }
        }




        public ConverterQuality quality;

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            int sourceSampleRate = waveFormatIn.SampleRate;
            int mySampleRate = waveFormatOut.SampleRate;

            int numTheirSamples = (int)Mathf.Floor((count * (float)sourceSampleRate / mySampleRate));

            int res = 100;
            try
            {
                res = ReadUnprocessedData(tempBuffer1, 0, numTheirSamples);
                if (res == 0)
                {
                    if (count == 0)
                    {
                        return 0;
                    }
                    else
                    {
                        return count;
                    }
                }
                int numOurSamples = (int)Mathf.Round((res * (float)mySampleRate / sourceSampleRate));
                res = src_simple_plain(tempBuffer1, tempBuffer2, res, numOurSamples, (float)mySampleRate / sourceSampleRate, quality, waveFormatOut.Channels);
                if (res > count)
                {
                    res = count;
                }
                Buffer.BlockCopy(tempBuffer2, 0, buffer, offset * sizeof(float), res * sizeof(float));
            }
            catch (Exception e)
            {
                Debug.Log("failed read: " + e.Message);
            }
            int fadeSize = 200;
            fadeSize = Mathf.Min(fadeSize, res - 1);
            if (res > fadeSize)
            {
                int startInd = offset + res - fadeSize;
                for (int i = 0; i < fadeSize; i++)
                {
                    float fade = 1 - (i / (float)(fadeSize - 1));
                    buffer[startInd + i] *= fade;
                    float startFade = 1 - fade;
                    buffer[offset + i] *= startFade;
                }
            }
            else
            {
                Debug.Log("not thing");
            }

            return Mathf.Max(res, 2);
        }

        public void Dispose()
        {

        }
    }
}