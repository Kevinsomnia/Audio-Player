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

    private const int REBUFFER_TIME = 5;

    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private string _initialURL;
    [SerializeField] private SampleRate _sampleRate = SampleRate.Hz_48000;


    [SerializeField] private float testTime = 0f;

    private int loopCount = 0;
    public int sampleFrontIdx = 0;
    public int sampleBackIdx = 0;

    private int syncStartIdx = 0;

    private int prevTimeSampleIdx = 0;
    private int initialChunk = 0;
    private int currNumBytes = 0;
    int totalBytes;
    int bytesPerSecond;
    short numChannels; int sampleRate; short bitsPerSample;

    private bool initial = true;


    private AudioClip _clip;

    private int _idx;
    private int _bufferLen;

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
            Debug.Log("len = " + res.ContentLength);
            Debug.Log(res.ContentType);
            MemoryStream ms = new MemoryStream();
            res.GetResponseStream().CopyTo(ms);
            byte[] buffer = ms.ToArray();
            numChannels = BitConverter.ToInt16(buffer, 22);
            sampleRate = BitConverter.ToInt32(buffer, 24);
            bytesPerSecond = BitConverter.ToInt32(buffer, 28);
            bitsPerSample = BitConverter.ToInt16(buffer, 34);
            totalBytes = BitConverter.ToInt32(buffer, 40);

            Debug.Log("numChannels = " + numChannels);
            Debug.Log("sampleRate = " + sampleRate);
            Debug.Log("subchunk2Size = " + totalBytes);
            Debug.Log("bitsPerSample = " + bitsPerSample);
            _bufferLen = sampleRate * 30;
            currNumBytes = 44;
            _clip = AudioClip.Create("", _bufferLen, numChannels, sampleRate, false);
            _audioSource.loop = true;
            _audioSource.clip = _clip;
            // StartCoroutine("PlayURL");
        }

    }

    public void Update()
    {
        testTime += Time.deltaTime;
        if (Mathf.Abs((float)(sampleBackIdx / sampleRate) - testTime) > 10f)
        {
            // sync
            int syncIdx = (int)(testTime * sampleRate);
            sampleFrontIdx = syncIdx;
            // loopCount = syncIdx / _bufferLen;
            _audioSource.timeSamples = 0;
            _audioSource.Pause();
            currNumBytes = (int)(syncIdx * bytesPerSecond) / sampleRate;
            syncStartIdx = syncIdx;
            prevTimeSampleIdx = 0;
        }

        if (_audioSource.timeSamples < prevTimeSampleIdx)
        {
            loopCount++;
        }
        prevTimeSampleIdx = _audioSource.timeSamples;

        sampleBackIdx = _bufferLen * loopCount + _audioSource.timeSamples + syncStartIdx;
        if (sampleFrontIdx - sampleBackIdx < sampleRate * 20)
        {
            int chunkSize = initial ? bytesPerSecond * 20 : bytesPerSecond * 5;
            HttpWebResponse res = GetStreamOfBytes(currNumBytes, currNumBytes + chunkSize);
            if (res.StatusCode == HttpStatusCode.PartialContent)
            {
                using (var currStream = new MemoryStream())
                {
                    res.GetResponseStream().CopyTo(currStream);
                    byte[] curr = currStream.ToArray();
                    float[] tmp = ConvertByteToFloat(curr, bitsPerSample);
                    // Push tmp to audio player
                    _idx = (sampleFrontIdx - syncStartIdx) % _bufferLen;
                    _clip.SetData(tmp, _idx);
                    sampleFrontIdx = (sampleFrontIdx + (int)(tmp.Length / numChannels));
                    currNumBytes += chunkSize;
                    Debug.Log("idx " + _idx);
                    Debug.Log(currNumBytes);
                    if (!_audioSource.isPlaying) _audioSource.UnPause();
                }
            }
        }
        if (initial)
        {
            _audioSource.Play();
            initial = false;
        }

    }

    public IEnumerator PlayURL()
    {
        int chunk = bytesPerSecond * 5;
        int initialChunk = bytesPerSecond * 6;
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
                    float last = tmp[tmp.Length - 1];
                    Debug.Log("idx " + _idx);
                    // Push tmp to audio player
                    _clip.SetData(tmp, _idx);
                    _idx = (_idx + (int)(tmp.Length / numChannels)) % _bufferLen;
                    currNumBytes += chunkSize;
                    Debug.Log(currNumBytes);
                }
            }
            if (initial)
            {
                _audioSource.Play();
            }
            initial = false;
            yield return new WaitForSecondsRealtime(5);
        } while (currNumBytes < totalBytes);
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
