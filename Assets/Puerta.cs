using UnityEngine;

public class Puerta : MonoBehaviour
{
    public float openAngle = 90f;
    public float speed = 2f;
    public string requiredKeyId = "";

    private bool isOpen = false;
    private bool isUnlocked = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    private void Awake()
    {
        ConfigureLockFromHierarchy();
    }

    void Start()
    {
        closedRotation = transform.rotation;
        openRotation = Quaternion.Euler(transform.eulerAngles + Vector3.up * openAngle);
    }

    void Update()
    {
        if (isOpen)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, openRotation, Time.deltaTime * speed);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, closedRotation, Time.deltaTime * speed);
        }
    }

    public string GetInteractionPrompt(InventarioJugador inventory)
    {
        if (!string.IsNullOrEmpty(requiredKeyId) && !isUnlocked &&
            (inventory == null || !inventory.HasItem(requiredKeyId)))
        {
            return "E - Puerta cerrada (falta llave)";
        }

        return isOpen ? "E - Cerrar puerta" : "E - Abrir puerta";
    }

    public bool TryToggleDoor(InventarioJugador inventory, out string feedback)
    {
        if (!string.IsNullOrEmpty(requiredKeyId) && !isUnlocked)
        {
            if (inventory == null || !inventory.HasItem(requiredKeyId))
            {
                feedback = GetLockedMessage();
                return false;
            }

            isUnlocked = true;
        }

        isOpen = !isOpen;
        feedback = isOpen ? "Puerta abierta" : "Puerta cerrada";
        return true;
    }

    public void ToggleDoor()
    {
        InventarioJugador inventory = FindFirstObjectByType<InventarioJugador>();
        if (!TryToggleDoor(inventory, out string feedback))
        {
            Debug.Log(feedback);
            return;
        }

        Debug.Log(feedback);
    }

    private void ConfigureLockFromHierarchy()
    {
        if (!string.IsNullOrEmpty(requiredKeyId))
            return;

        Transform current = transform;
        while (current != null)
        {
            string objectName = current.name.ToLowerInvariant();
            if (objectName.Contains("entrada_izquierda") || objectName.Contains("entrada_derecha"))
            {
                requiredKeyId = InventarioJugador.LlaveEntradaId;
                return;
            }

            if (objectName.Contains("puerta_tocador"))
            {
                requiredKeyId = InventarioJugador.LlaveTocadorId;
                return;
            }

            current = current.parent;
        }
    }

    private string GetLockedMessage()
    {
        if (requiredKeyId == InventarioJugador.LlaveEntradaId)
            return "Puerta cerrada: necesitas la llave de entrada";

        if (requiredKeyId == InventarioJugador.LlaveTocadorId)
            return "Puerta cerrada: necesitas la llave del tocador";

        return "Puerta cerrada: necesitas la llave correspondiente";
    }
}
