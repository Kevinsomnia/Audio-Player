using UnityEngine;

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
            PlayURL(_initialURL);
        }
    }

    public void PlayURL(string url)
    {
        _audioSource.Play();
    }

    private void OnAudioRead(float[] data)
    {

    }
}
