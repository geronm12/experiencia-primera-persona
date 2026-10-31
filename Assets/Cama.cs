using UnityEngine;

public class Cama : MonoBehaviour
{
    public FadeScreen fadeScreen;
    public PlayerMovement player;

    public void Dormir()
    {
        Debug.Log("Durmiendo...");

        player.enabled = false; 
        fadeScreen.FadeToBlack();
    }
}