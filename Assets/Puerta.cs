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
    private float openingDirection = 1f;

    private void Awake()
    {
        ConfigureLockFromHierarchy();
    }

    void Start()
    {
        closedRotation = transform.rotation;
        openingDirection = GetInwardOpeningDirection();
        openRotation = Quaternion.Euler(transform.eulerAngles + Vector3.up * Mathf.Abs(openAngle) * openingDirection);
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

        bool shouldOpen = !isOpen;
        Puerta[] group = GetEntranceDoorGroup();
        if (group.Length > 1)
        {
            foreach (Puerta leaf in group)
            {
                if (leaf == null) continue;
                leaf.isUnlocked = true;
                leaf.isOpen = shouldOpen;
            }
        }
        else
        {
            isOpen = shouldOpen;
        }
        feedback = isOpen ? "Puerta abierta" : "Puerta cerrada";
        return true;
    }

    public Transform[] GetLinkedDoorTransforms()
    {
        Puerta[] group = GetEntranceDoorGroup();
        System.Collections.Generic.List<Transform> linked = new System.Collections.Generic.List<Transform>();
        foreach (Puerta leaf in group)
            if (leaf != null && leaf != this) linked.Add(leaf.transform);
        return linked.ToArray();
    }

    private Puerta[] GetEntranceDoorGroup()
    {
        if (GetEntranceSide(transform) == 0)
            return new[] { this };

        System.Collections.Generic.List<Puerta> group = new System.Collections.Generic.List<Puerta>();
        foreach (Puerta candidate in FindObjectsByType<Puerta>(FindObjectsSortMode.None))
        {
            if (candidate != null && GetEntranceSide(candidate.transform) != 0)
                group.Add(candidate);
        }
        return group.ToArray();
    }

    private int GetEntranceSide(Transform leaf)
    {
        Transform current = leaf;
        while (current != null)
        {
            string objectName = current.name.ToLowerInvariant();
            if (objectName.Contains("entrada_izquierda")) return -1;
            if (objectName.Contains("entrada_derecha")) return 1;
            current = current.parent;
        }
        return 0;
    }

    private float GetInwardOpeningDirection()
    {
        if (GetEntranceSide(transform) == 0) return Mathf.Sign(openAngle == 0f ? 1f : openAngle);

        Puerta[] group = GetEntranceDoorGroup();
        Vector3 midpoint = Vector3.zero;
        int count = 0;
        foreach (Puerta leaf in group)
        {
            if (leaf == null) continue;
            midpoint += leaf.transform.position;
            count++;
        }
        if (count == 0) return 1f;
        midpoint /= count;

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player == null) return 1f;
        Vector3 inward = player.transform.position - midpoint;
        inward.y = 0f;
        if (inward.sqrMagnitude < 0.001f) return 1f;
        inward.Normalize();

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        Vector3 hingeToLeaf = hasBounds ? bounds.center - transform.position : transform.right;
        hingeToLeaf.y = 0f;
        if (hingeToLeaf.sqrMagnitude < 0.001f) hingeToLeaf = transform.right;

        float angle = Mathf.Abs(openAngle);
        Vector3 positiveMovement = Quaternion.AngleAxis(angle, Vector3.up) * hingeToLeaf - hingeToLeaf;
        Vector3 negativeMovement = Quaternion.AngleAxis(-angle, Vector3.up) * hingeToLeaf - hingeToLeaf;
        return Vector3.Dot(positiveMovement, inward) >= Vector3.Dot(negativeMovement, inward) ? 1f : -1f;
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
