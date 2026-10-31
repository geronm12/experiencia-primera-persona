using UnityEngine;

public class Gramophone : MonoBehaviour
{
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void ToggleMusic()
    {
        if (audioSource.isPlaying)
            audioSource.Stop();
        else
            audioSource.Play();
    }
}