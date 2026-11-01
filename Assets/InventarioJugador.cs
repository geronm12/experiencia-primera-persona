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
    private readonly List<string> itemOrder = new List<string>();
    private readonly Dictionary<string, string> displayNames = new Dictionary<string, string>();

    public int Count => itemOrder.Count;
    public IReadOnlyList<string> ItemIds => itemOrder;

    public bool HasItem(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && itemIds.Contains(itemId);
    }

    public bool TryAddItem(string itemId, string displayName)
    {
        if (string.IsNullOrEmpty(itemId) || !itemIds.Add(itemId))
            return false;

        itemOrder.Add(itemId);
        itemNames.Add(displayName);
        displayNames[itemId] = displayName;
        return true;
    }

    public string GetDisplayName(string itemId)
    {
        return displayNames.TryGetValue(itemId, out string displayName) ? displayName : itemId;
    }

    public string GetDescription(string itemId)
    {
        switch (itemId)
        {
            case AnteojosId:
                return "Presiona Z, X o C para aplicar los filtros de los anteojos. ¡Pueden revelarse objetos ocultos!";
            case LlaveEntradaId:
                return "Llave de la puerta frontal de la casa.";
            case LlaveTocadorId:
                return "Llave de la puerta del baño.";
            case CelularId:
                return "Celular para apagar la televisión. Se revela con el filtro C.";
            default:
                return "";
        }
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
