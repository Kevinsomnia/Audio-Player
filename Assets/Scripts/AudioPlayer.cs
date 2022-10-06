using System;
using System.IO;
using System.Net;
using UnityEngine;

public class AudioPlayer : MonoBehaviour
{

    private const float RESYNC_TIME = 2f;
    private const int AUDIO_CLIP_TIME = 30;
    private const int CHUNK_SIZE = 5;

    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private string _initialURL;
    [SerializeField] private float testTime = 0f;

    public int loopCount = 0;
    public int loadedIdx = 0;
    public int playedIdx = 0;
    private int syncLoadedIdx = 0;
    private int prevTimeSampleIdx = 0;
    private int initialChunk = 0;
    private int currNumBytes = 0;
    int totalBytes;
    int bytesPerSecond;
    short numChannels; int sampleRate; short bitsPerSample;

    private float[] samples;
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
            Debug.Log("bytesPerSecond = " + bytesPerSecond);
            _bufferLen = sampleRate * AUDIO_CLIP_TIME; // Audio Clip is 30 seconds long
            currNumBytes = 44;
            _clip = AudioClip.Create("AUDIO PLAYER", _bufferLen, numChannels, sampleRate, false);
            _audioSource.loop = true;
            _audioSource.clip = _clip;
            samples = new float[_bufferLen * numChannels];
            // StartCoroutine("PlayURL");
        }

    }

    public void Update()
    {
        testTime += Time.deltaTime;
        if (Mathf.Abs((float)(playedIdx / sampleRate) - testTime) > RESYNC_TIME)
        {
            // sync
            syncLoadedIdx = (int)(testTime * sampleRate);
            loadedIdx = syncLoadedIdx;
            loopCount = 0;
            _audioSource.timeSamples = 0;
            _audioSource.Pause();
            currNumBytes = syncLoadedIdx * (bytesPerSecond / sampleRate);
            prevTimeSampleIdx = 0;
        }

        if (_audioSource.timeSamples < prevTimeSampleIdx)
        {
            loopCount++;
        }
        prevTimeSampleIdx = _audioSource.timeSamples;

        playedIdx = _bufferLen * loopCount + _audioSource.timeSamples + syncLoadedIdx;
        if (loadedIdx - playedIdx < sampleRate * 20)
        {
            int chunkSize = bytesPerSecond * CHUNK_SIZE * (initial ? 4 : 1);
            HttpWebResponse res = GetStreamOfBytes(currNumBytes, currNumBytes + chunkSize);
            if (res.StatusCode == HttpStatusCode.PartialContent)
            {
                using (var currStream = new MemoryStream())
                {
                    res.GetResponseStream().CopyTo(currStream);
                    byte[] curr = currStream.ToArray();
                    float[] tmp = ConvertByteToFloat(curr, bitsPerSample);
                    _idx = (loadedIdx - syncLoadedIdx) % _bufferLen;
                    // _clip.SetData(tmp, _idx);
                    int endIdx = _idx * numChannels + tmp.Length;
                    int outOfBounds = endIdx - samples.Length;
                    if (endIdx >= samples.Length)
                    {
                        Array.Copy(tmp, 0, samples, _idx * numChannels, tmp.Length - outOfBounds);
                        Array.Copy(tmp, tmp.Length-outOfBounds, samples, 0, outOfBounds);
                    }
                    else
                    {
                        Array.Copy(tmp, 0, samples, _idx * numChannels, tmp.Length);
                    }
                    _clip.SetData(samples, 0);
                    loadedIdx += (int)(tmp.Length / numChannels);
                    currNumBytes += chunkSize;
                    // Debug.Log(currNumBytes);
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
        if (bitsPerSample == (short)16)
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
