using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FadeScreen : MonoBehaviour
{
    public Image fadeImage;
    public float speed = 2f;

    private bool isFading = false;
    private bool fadingFromBlack;
    private bool isBlack = false;
    private bool canRestart = false;
    private float alpha = 1f;

    private void Start()
    {
        FadeFromBlack();
    }

    void Update()
    {
        if (isFading)
        {
            float targetAlpha = fadingFromBlack ? 0f : 1f;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, speed * Time.deltaTime);
            ApplyAlpha();

            if (Mathf.Approximately(alpha, targetAlpha))
            {
                isFading = false;
                isBlack = !fadingFromBlack;

                if (isBlack)
                    Invoke(nameof(EnableRestart), 0.5f);
            }
        }

        if (isBlack && canRestart && Input.GetMouseButtonDown(0))
        {
            SceneManager.LoadScene("TitleScene");
        }
    }

    void EnableRestart()
    {
        canRestart = true;
    }

    public void FadeToBlack()
    {
        if (fadeImage != null)
            alpha = fadeImage.color.a;
        isFading = true;
        fadingFromBlack = false;
    }

    public void FadeFromBlack()
    {
        alpha = 1f;
        ApplyAlpha();
        isBlack = false;
        canRestart = false;
        fadingFromBlack = true;
        isFading = true;
    }

    private void ApplyAlpha()
    {
        if (fadeImage == null)
            return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }

    private void OnGUI()
    {
        if (fadeImage != null || alpha <= 0f)
            return;

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }
}
