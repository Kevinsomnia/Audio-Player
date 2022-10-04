using System.Collections;
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

        int sampleRate = (int)_sampleRate;
        _clip = AudioClip.Create(
            "StreamClip",
            lengthSamples: sampleRate * CLIP_LENGTH_SECONDS,
            channels: 2,
            frequency: sampleRate,
            stream: true,
            pcmreadercallback: OnAudioRead
        );
        _audioSource.clip = _clip;
        _audioSource.loop = true;

        if (!string.IsNullOrEmpty(_initialURL))
        {
            // PlayURL(_initialURL);
        }

    }

    public void PlayURL(string url, AudioType audioType)
    {
        StartCoroutine(Start(url, audioType));
    }

    private void OnAudioRead(float[] data)
    {

    }

    private IEnumerator Start(string url, AudioType audioType)
    {
        using (var webRequest = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            ((DownloadHandlerAudioClip)webRequest.downloadHandler).streamAudio = true;

            webRequest.SendWebRequest();
            while (webRequest.result != UnityWebRequest.Result.ConnectionError && webRequest.downloadedBytes < 1024)
                // Ensure that we have at least 1 byte to stream
                yield return null;

            if (webRequest.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError(webRequest.error);
                yield break;
            }

            var clip = ((DownloadHandlerAudioClip)webRequest.downloadHandler).audioClip;
            _audioSource.clip = clip;
            _audioSource.Play();
        }
    }
}
