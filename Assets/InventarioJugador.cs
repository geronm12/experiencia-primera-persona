using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class InventarioJugador : MonoBehaviour
{
    public const string LlaveEntradaId = "llave_entrada";
    public const string LlaveTocadorId = "llave_tocador";
    public const string AnteojosId = "anteojos_de_sol";
    public const string CelularId = "celular";

    private readonly HashSet<string> itemIds = new HashSet<string>();
    private readonly List<string> itemNames = new List<string>();

    public int Count => itemNames.Count;

    public bool HasItem(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && itemIds.Contains(itemId);
    }

    public bool TryAddItem(string itemId, string displayName)
    {
        if (string.IsNullOrEmpty(itemId) || !itemIds.Add(itemId))
            return false;

        itemNames.Add(displayName);
        return true;
    }

    public string GetContentsText()
    {
        if (itemNames.Count == 0)
            return "Inventario\nVacío";

        StringBuilder contents = new StringBuilder("Inventario");
        foreach (string itemName in itemNames)
            contents.Append("\n• ").Append(itemName);

        return contents.ToString();
    }
}
