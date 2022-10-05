using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AudioPlayer : MonoBehaviour
{
    public enum SampleRate
    {
        Hz_32000 = 32000,
        Hz_44100 = 44100,
        Hz_48000 = 48000,
    }

    private const int CLIP_LENGTH_SECONDS = 5;

    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private string _initialURL;
    [SerializeField] private SampleRate _sampleRate = SampleRate.Hz_48000;

    private AudioClip _clip;


    private void Awake()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (!string.IsNullOrEmpty(_initialURL))
        {
            // Parsing WAV Header for audio stuff
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(_initialURL);
            req.AddRange(0, 44);
            req.KeepAlive = true;
            HttpWebResponse res = (HttpWebResponse)req.GetResponse();
            if (res.StatusCode != HttpStatusCode.PartialContent) return;
            Debug.Log("len = " + res.ContentLength);
            Debug.Log(res.ContentType);
            MemoryStream ms = new MemoryStream();
            res.GetResponseStream().CopyTo(ms);
            byte[] buffer = ms.ToArray();
            short numChannels = BitConverter.ToInt16(buffer, 22);
            int sampleRate = BitConverter.ToInt32(buffer, 24);
            int byteRate = BitConverter.ToInt32(buffer, 28);
            short bitsPerSample = BitConverter.ToInt16(buffer, 34);
            int subchunk2Size = BitConverter.ToInt32(buffer, 40);

            Debug.Log("numChannels = " + numChannels);
            Debug.Log("sampleRate = " + sampleRate);
            Debug.Log("subchunk2Size = " + subchunk2Size);
            Debug.Log("bitsPerSample = " + bitsPerSample);
            StartCoroutine(PlayURL(_initialURL, subchunk2Size, byteRate, numChannels, sampleRate, bitsPerSample));
        }

    }

    public IEnumerator PlayURL(string url, int totalBytes, int bytesPerSecond, int numChannels, int sampleRate, short bitsPerSample)
    {
        int currNumBytes = 44;
        int chunk = bytesPerSecond * 5;
        int initialChunk = bytesPerSecond * 20;
        bool initial = true;
        float[] f = new float[(int)(totalBytes / Math.Ceiling(bitsPerSample / 8.0))];
        int floatCounter = 0;
        do
        {
            int chunkSize = initial ? initialChunk : chunk;
            HttpWebResponse res = GetStreamOfBytes(currNumBytes, currNumBytes + chunkSize);
            if (res.StatusCode == HttpStatusCode.PartialContent)
            {
                using (var currStream = new MemoryStream())
                {
                    res.GetResponseStream().CopyTo(currStream);
                    byte[] curr = currStream.ToArray();
                    float[] tmp = ConvertByteToFloat(curr, bitsPerSample);
                    // Push tmp to audio player
                    int len = tmp.Length + floatCounter > f.Length ? tmp.Length + floatCounter - f.Length : tmp.Length;
                    Array.Copy(tmp, 0, f, floatCounter, len);
                    floatCounter += tmp.Length;
                    currNumBytes += chunkSize;
                    Debug.Log(currNumBytes);
                }
            }
            initial = false;
            yield return new WaitForSecondsRealtime(5);
        } while (currNumBytes < totalBytes);
        // This isn't needed this is just for testing
        AudioClip clip = AudioClip.Create("ClipName", f.Length, numChannels, sampleRate, false);
        clip.SetData(f, 0);
        Debug.Log("PLAYING");
        _audioSource.clip = clip;
        _audioSource.Play();
    }

    private HttpWebResponse GetStreamOfBytes(int rangeStart, int rangeEnd)
    {
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(_initialURL);
        req.AddRange(rangeStart, rangeEnd);
        req.KeepAlive = true;
        return (HttpWebResponse)req.GetResponse();
    }

    private float[] ConvertByteToFloat(byte[] array, short bitsPerSample)
    {
        float[] floatArr;
        if (bitsPerSample == 16)
        {
            short[] sdata = new short[(int)Math.Ceiling(array.Length / 2.0)];
            Buffer.BlockCopy(array, 0, sdata, 0, array.Length);
            floatArr = new float[sdata.Length];
            for (int i = 0; i < floatArr.Length; i++)
            {
                floatArr[i] = ((float)sdata[i] / short.MaxValue);
            }
        }
        else
        {
            char[] cdata = System.Text.Encoding.UTF8.GetString(array).ToCharArray();
            floatArr = new float[cdata.Length];
            for (int i = 0; i < floatArr.Length; i++)
            {
                floatArr[i] = ((float)cdata[i] / char.MaxValue);
            }
        }
        return floatArr;
    }
}
