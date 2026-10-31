using TMPro;
using UnityEngine;

public class BlinkText : MonoBehaviour
{
    public float blinkSpeed = 2f;

    private TextMeshProUGUI textComponent;

    void Start()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        Color color = textComponent.color;
        color.a = Mathf.PingPong(Time.time * blinkSpeed, 1f);
        textComponent.color = color;
    }
}