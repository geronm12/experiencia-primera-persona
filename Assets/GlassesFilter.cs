using UnityEngine;
using UnityEngine.UI;

public class GlassesFilter : MonoBehaviour
{
    public Image filterImage;

    private KeyCode activeFilter = KeyCode.None;
    private Color currentFilterColor = new Color(0f, 0f, 0f, 0f);
    private InventarioJugador inventory;
    private Interaction interaction;

    void Update()
    {
        if (interaction == null)
            interaction = GetComponent<Interaction>();

        if (interaction != null && interaction.IsInventoryOpen)
            return;

        if (Input.GetKeyDown(KeyCode.X))
            ToggleFilter(KeyCode.X, new Color(1f, 0.85f, 0.5f, 0.25f));
        else if (Input.GetKeyDown(KeyCode.Y))
            ToggleFilter(KeyCode.Y, new Color(0.6f, 0.8f, 1f, 0.25f));
        else if (Input.GetKeyDown(KeyCode.Z))
            ToggleFilter(KeyCode.Z, new Color(1f, 0.45f, 0.68f, 0.3f));
        else if (Input.GetKeyDown(KeyCode.C))
            ToggleFilter(KeyCode.C, new Color(0.85f, 0.55f, 1f, 0.3f));
    }

    public bool IsFilterActive(KeyCode filterKey)
    {
        return activeFilter == filterKey;
    }

    private void ToggleFilter(KeyCode filterKey, Color filterColor)
    {
        if (inventory == null)
            inventory = GetComponent<InventarioJugador>();

        if (inventory == null || !inventory.HasItem(InventarioJugador.AnteojosId))
        {
            if (interaction == null)
                interaction = GetComponent<Interaction>();

            if (interaction != null)
                interaction.ShowMessage("Necesitas los anteojos de sol para usar los filtros");

            return;
        }

        if (activeFilter == filterKey)
        {
            activeFilter = KeyCode.None;
            currentFilterColor = new Color(0f, 0f, 0f, 0f);
        }
        else
        {
            activeFilter = filterKey;
            currentFilterColor = filterColor;
        }

        if (filterImage != null)
            filterImage.color = currentFilterColor;
    }

    private void OnGUI()
    {
        if (filterImage != null || activeFilter == KeyCode.None)
            return;

        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;
        GUI.depth = 100;
        GUI.color = currentFilterColor;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }
}
