using UnityEngine;
using UnityEngine.UI;

public class GlassesFilter : MonoBehaviour
{
    public Image filterImage;

    private int currentFilter = 0;

    void Update()
    {
        ToggleFilter(KeyCode.Z, 1, new Color(1f, 0.85f, 0.5f, 0.25f));
        ToggleFilter(KeyCode.X, 2, new Color(0.6f, 0.8f, 1f, 0.25f));
        ToggleFilter(KeyCode.C, 3, new Color(0f, 0f, 0f, 0.35f));
    }

    void ToggleFilter(KeyCode key, int filterId, Color filterColor)
    {
        if (!Input.GetKeyDown(key))
            return;

        // Si ya estaba puesto ese filtro, lo quitamos
        if (currentFilter == filterId)
        {
            filterImage.color = new Color(0f, 0f, 0f, 0f);
            currentFilter = 0;
        }
        else
        {
            filterImage.color = filterColor;
            currentFilter = filterId;
        }
    }

}
