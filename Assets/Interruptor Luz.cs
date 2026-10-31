using UnityEngine;

public class InterruptorLuz : MonoBehaviour
{
    public GameObject luz; 

    private bool encendida = true;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void ToggleLuz()
    {
        audioSource.Play();

        encendida = !encendida;

        luz.SetActive(encendida);

        Debug.Log(encendida ? "Luz encendida" : "Luz apagada");
    }
}