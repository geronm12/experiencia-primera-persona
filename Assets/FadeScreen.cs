using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FadeScreen : MonoBehaviour
{
    public Image fadeImage;
    public float speed = 2f;

    private bool isFading = false;
    private bool isBlack = false;
    private bool canRestart = false;

    void Update()
    {
        if (isFading)
        {
            Color c = fadeImage.color;
            c.a = Mathf.MoveTowards(c.a, 1f, speed * Time.deltaTime);
            fadeImage.color = c;

            if (c.a >= 1f)
            {
                isFading = false;
                isBlack = true;

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
        isFading = true;
    }
}