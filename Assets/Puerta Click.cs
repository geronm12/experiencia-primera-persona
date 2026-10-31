using System.Collections.Generic;
using UnityEngine;

public class Interaction : MonoBehaviour
{
    public float distance = 3f;
    public KeyCode interactionKey = KeyCode.E;
    public Color highlightColor = new Color(0.35f, 0.9f, 1f, 1f);

    private Camera playerCamera;
    private InventarioJugador inventory;
    private InteractableTarget currentTarget;
    private string message;
    private float messageUntil;
    private bool introVisible = true;
    private float introUntil;
    private bool inventoryOpen;
    private bool papersRead;
    private string pendingTasksMessage;
    private float pendingTasksUntil;
    private GUIStyle promptStyle;
    private GUIStyle messageStyle;
    private GUIStyle introStyle;
    private readonly Dictionary<Renderer, MaterialPropertyBlock> originalBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
    private readonly List<Transform> highlightOnlyTargets = new List<Transform>();
    private readonly List<FilterHiddenTarget> filterHiddenTargets = new List<FilterHiddenTarget>();
    private MaterialPropertyBlock highlightBlock;
    private GlassesFilter glassesFilter;
    private int baseColorId;
    private int colorId;
    private int emissionColorId;
    private const string IntroText = "Vuelves a casa después de una larga jornada de trabajar, tu misión es ir acostarte a dormir para poder descansar correctamente";

    private static readonly Dictionary<string, string> namedObjectPrompts = new Dictionary<string, string>
    {
        { "Celular", "E - Recoger" },
        { "Llave_Entrada", "E - Recoger" },
        { "Llave_Tocador", "E - Recoger" },
        { "Anteojos_de_sol", "E - Recoger" },
        { "Televisor", "E - Prender / apagar" },
        { "Parlante_Izquierdo", "E - Prender / apagar" },
        { "Parlante_Derecho", "E - Prender / apagar" },
        { "Papers", "E - Leer" },
        { "Cepillo_dientes", "E - Lavar dientes" },
        // Keep the current serialized scene working until all renamed objects are saved.
        { "cellphone", "E - Recoger" },
        { "Sunglasses_2", "E - Recoger" },
        { "Key3", "E - Recoger" },
        { "Key3 (1)", "E - Recoger" },
        { "TV_Apt_01", "E - Prender / apagar" },
        { "SS_Stand_01 (1)", "E - Prender / apagar" },
        { "SS_Stand_01 (3)", "E - Prender / apagar" },
        { "SS_Stand_01 (4)", "E - Prender / apagar" },
        { "SS_Stand_01 (2)", "E - Prender / apagar" }
    };

    private class FilterHiddenTarget
    {
        public Transform root;
        public KeyCode requiredFilter;
        public Renderer[] renderers;
        public bool[] rendererWasEnabled;
        public Collider[] colliders;
        public bool[] colliderWasEnabled;
        public bool isVisible;
    }

    private class InteractableTarget
    {
        public Component component;
        public Transform visualRoot;
        public string prompt;
        public bool canInteract = true;
        public bool isOccluded;
    }

    private void Awake()
    {
        highlightBlock = new MaterialPropertyBlock();
        baseColorId = Shader.PropertyToID("_BaseColor");
        colorId = Shader.PropertyToID("_Color");
        emissionColorId = Shader.PropertyToID("_EmissionColor");

        // The legacy scene also has this component on some doors. Only the player
        // should run the shared raycast, otherwise several copies interact at once.
        if (GetComponent<PlayerMovement>() == null)
        {
            enabled = false;
            return;
        }

        inventory = GetComponent<InventarioJugador>();
        if (inventory == null)
            inventory = gameObject.AddComponent<InventarioJugador>();

        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
            playerCamera = Camera.main;
        glassesFilter = GetComponent<GlassesFilter>();

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (namedObjectPrompts.ContainsKey(sceneTransform.name))
                highlightOnlyTargets.Add(sceneTransform);

            KeyCode requiredFilter = GetRequiredFilter(sceneTransform.name);
            if (requiredFilter == KeyCode.None)
                continue;

            Renderer[] renderers = sceneTransform.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = sceneTransform.GetComponentsInChildren<Collider>(true);
            FilterHiddenTarget hiddenTarget = new FilterHiddenTarget
            {
                root = sceneTransform,
                requiredFilter = requiredFilter,
                renderers = renderers,
                rendererWasEnabled = GetRendererEnabledStates(renderers),
                colliders = colliders,
                colliderWasEnabled = GetColliderEnabledStates(colliders),
                isVisible = true
            };
            filterHiddenTargets.Add(hiddenTarget);
        }
    }

    private void Start()
    {
        introUntil = Time.unscaledTime + 10f;
    }

    private void Update()
    {
        if (playerCamera == null)
        {
            ClearTarget();
            return;
        }

        UpdateFilterHiddenTargets();

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        InteractableTarget nextTarget = null;
        float obstructionDistance = distance;
        if (Physics.Raycast(ray, out RaycastHit hit, distance))
        {
            nextTarget = FindTarget(hit.collider);
            obstructionDistance = hit.distance;
        }

        InteractableTarget boundsTarget = FindNamedTargetByBounds(ray, obstructionDistance, distance);
        if (boundsTarget != null)
            nextTarget = boundsTarget;

        if (!SameTarget(currentTarget, nextTarget))
        {
            RestoreHighlight();
            currentTarget = nextTarget;
            ApplyHighlight();
        }

        bool pressedInteract = Input.GetKeyDown(interactionKey) || Input.GetMouseButtonDown(0);
        if (Input.GetKeyDown(KeyCode.Tab))
            inventoryOpen = !inventoryOpen;

        if (introVisible)
        {
            if (Time.unscaledTime >= introUntil || pressedInteract)
                introVisible = false;

            return;
        }

        if (currentTarget != null && pressedInteract)
        {
            if (!currentTarget.canInteract)
            {
                ShowMessage("Acércate al celular para recogerlo");
                return;
            }

            Interact(currentTarget.component);
        }
    }

    private InteractableTarget FindTarget(Collider hitCollider)
    {
        Puerta door = hitCollider.GetComponentInParent<Puerta>();
        if (door != null)
            return MakeTarget(door, door.transform, door.GetInteractionPrompt(inventory));

        InterruptorLuz lightSwitch = hitCollider.GetComponentInParent<InterruptorLuz>();
        if (lightSwitch != null)
            return MakeTarget(lightSwitch, lightSwitch.transform, "E - Usar interruptor");

        Cama bed = hitCollider.GetComponentInParent<Cama>();
        if (bed != null)
            return MakeTarget(bed, bed.transform, "E - Dormir");

        Gramophone gramophone = hitCollider.GetComponentInParent<Gramophone>();
        if (gramophone != null)
            return MakeTarget(gramophone, gramophone.transform, "E - Reproducir / detener música");

        SojaExiles.opencloseDoor assetDoor = hitCollider.GetComponentInParent<SojaExiles.opencloseDoor>();
        if (assetDoor != null)
            return MakeTarget(assetDoor, assetDoor.transform, "Clic - Abrir / cerrar");

        SojaExiles.opencloseDoor1 rightOpeningDoor = hitCollider.GetComponentInParent<SojaExiles.opencloseDoor1>();
        if (rightOpeningDoor != null)
            return MakeTarget(rightOpeningDoor, rightOpeningDoor.transform, "Clic - Abrir / cerrar");

        Transform namedObject = FindNamedHighlightObject(hitCollider.transform);
        if (namedObject != null)
            return MakeTarget(namedObject, namedObject, namedObjectPrompts[namedObject.name]);

        return null;
    }

    private Transform FindNamedHighlightObject(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current != null)
        {
            if (namedObjectPrompts.ContainsKey(current.name) && CanTargetWithCurrentFilter(current.name))
                return current;

            current = current.parent;
        }

        return null;
    }

    private InteractableTarget FindNamedTargetByBounds(Ray ray, float obstructionDistance, float maxDistance)
    {
        Transform closestTarget = null;
        float closestDistance = maxDistance;
        bool closestTargetIsOccluded = false;

        foreach (Transform target in highlightOnlyTargets)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                continue;

            if (!CanTargetWithCurrentFilter(target.name))
                continue;

            bool phoneCanBeSeenThroughObjects = IsCellphone(target.name) &&
                                                glassesFilter != null &&
                                                glassesFilter.IsFilterActive(KeyCode.C);
            float allowedDistance = phoneCanBeSeenThroughObjects ? maxDistance : obstructionDistance;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                continue;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            if (bounds.IntersectRay(ray, out float hitDistance) &&
                hitDistance >= 0f && hitDistance < allowedDistance && hitDistance < closestDistance)
            {
                closestTarget = target;
                closestDistance = hitDistance;
                closestTargetIsOccluded = hitDistance > obstructionDistance;
            }
        }

        if (closestTarget == null)
            return null;

        string prompt = closestTargetIsOccluded
            ? "Celular detectado con C"
            : namedObjectPrompts[closestTarget.name];
        return MakeTarget(closestTarget, closestTarget, prompt, !closestTargetIsOccluded,
            closestTargetIsOccluded);
    }

    private InteractableTarget MakeTarget(Component component, Transform visualRoot, string prompt,
        bool canInteract = true, bool isOccluded = false)
    {
        return new InteractableTarget
        {
            component = component,
            visualRoot = visualRoot,
            prompt = prompt,
            canInteract = canInteract,
            isOccluded = isOccluded
        };
    }

    private bool SameTarget(InteractableTarget a, InteractableTarget b)
    {
        return a == null ? b == null : b != null && a.component == b.component;
    }

    private void Interact(Component component)
    {
        if (component is Puerta door)
        {
            door.TryToggleDoor(inventory, out string feedback);
            ShowMessage(feedback);
        }
        else if (component is InterruptorLuz lightSwitch)
        {
            lightSwitch.ToggleLuz();
            ShowMessage("Usaste el interruptor");
        }
        else if (component is Cama bed)
        {
            TrySleep(bed);
        }
        else if (component is Gramophone gramophone)
        {
            gramophone.ToggleMusic();
            ShowMessage("Usaste el gramófono");
        }
        else if (component is Transform target)
        {
            if (target.name == "Papers")
            {
                papersRead = true;
                ShowMessage("Dejé las llaves de la puerta de casa en el banco de atrás, para poder encontrarlas deberás buscar los anteojos de sol", 8f);
            }
            else if (target.name == "Cepillo_dientes")
            {
                ShowMessage("Te lavaste los dientes", 3f);
            }
            else
            {
                TryPickUp(target);
            }
        }
    }

    private void TryPickUp(Transform target)
    {
        if (target == null || !TryGetPickup(target.name, out string itemId, out string displayName))
            return;

        KeyCode requiredFilter = GetSelectionFilter(target.name);
        if (requiredFilter != KeyCode.None &&
            (glassesFilter == null || !glassesFilter.IsFilterActive(requiredFilter)))
        {
            ShowMessage("Activa el filtro correcto para recoger este objeto");
            return;
        }

        if (!inventory.TryAddItem(itemId, displayName))
        {
            ShowMessage(displayName + " ya está en el inventario");
            return;
        }

        target.gameObject.SetActive(false);
        ShowMessage("Recogiste: " + displayName);
    }

    private void TrySleep(Cama bed)
    {
        List<string> pending = new List<string>();
        if (!papersRead)
            pending.Add("Leer los papeles");
        if (!inventory.HasItem(InventarioJugador.AnteojosId))
            pending.Add("Encontrar y recoger los anteojos de sol");
        if (!inventory.HasItem(InventarioJugador.LlaveEntradaId))
            pending.Add("Encontrar y recoger la llave de entrada");
        if (!inventory.HasItem(InventarioJugador.LlaveTocadorId))
            pending.Add("Encontrar y recoger la llave del baño");
        if (!inventory.HasItem(InventarioJugador.CelularId))
            pending.Add("Encontrar y recoger el celular");

        if (pending.Count > 0)
        {
            pendingTasksMessage = "Antes de dormir, termina estos pendientes:\n\n• " + string.Join("\n• ", pending);
            pendingTasksUntil = Time.unscaledTime + 12f;
            return;
        }

        bed.Dormir();
        ShowMessage("Completaste los pendientes. Buen descanso.", 3f);
    }

    private bool TryGetPickup(string objectName, out string itemId, out string displayName)
    {
        switch (objectName)
        {
            case "Llave_Entrada":
            case "Key3":
                itemId = InventarioJugador.LlaveEntradaId;
                displayName = "Llave de entrada";
                return true;
            case "Llave_Tocador":
            case "Key3 (1)":
                itemId = InventarioJugador.LlaveTocadorId;
                displayName = "Llave del tocador";
                return true;
            case "Anteojos_de_sol":
            case "Sunglasses_2":
                itemId = InventarioJugador.AnteojosId;
                displayName = "Anteojos de sol";
                return true;
            case "Celular":
            case "cellphone":
                itemId = InventarioJugador.CelularId;
                displayName = "Celular";
                return true;
            default:
                itemId = null;
                displayName = null;
                return false;
        }
    }

    public void ShowMessage(string text, float duration = 1.5f)
    {
        message = text;
        messageUntil = Time.unscaledTime + duration;
    }

    private void UpdateFilterHiddenTargets()
    {
        foreach (FilterHiddenTarget target in filterHiddenTargets)
        {
            if (target.root == null || !target.root.gameObject.activeInHierarchy)
                continue;

            bool shouldBeVisible = glassesFilter != null &&
                                   glassesFilter.IsFilterActive(target.requiredFilter);
            if (target.isVisible == shouldBeVisible)
                continue;

            for (int i = 0; i < target.renderers.Length; i++)
            {
                if (target.renderers[i] != null)
                    target.renderers[i].enabled = shouldBeVisible && target.rendererWasEnabled[i];
            }

            for (int i = 0; i < target.colliders.Length; i++)
            {
                if (target.colliders[i] != null)
                    target.colliders[i].enabled = shouldBeVisible && target.colliderWasEnabled[i];
            }

            target.isVisible = shouldBeVisible;
        }
    }

    private bool[] GetRendererEnabledStates(Renderer[] renderers)
    {
        bool[] enabledStates = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            enabledStates[i] = renderers[i] != null && renderers[i].enabled;

        return enabledStates;
    }

    private bool[] GetColliderEnabledStates(Collider[] colliders)
    {
        bool[] enabledStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
            enabledStates[i] = colliders[i] != null && colliders[i].enabled;

        return enabledStates;
    }

    private KeyCode GetRequiredFilter(string objectName)
    {
        switch (objectName)
        {
            case "Llave_Entrada":
            case "Key3":
                return KeyCode.Z;
            case "Llave_Tocador":
            case "Key3 (1)":
                return KeyCode.X;
            default:
                return KeyCode.None;
        }
    }

    private KeyCode GetSelectionFilter(string objectName)
    {
        return GetRequiredFilter(objectName);
    }

    private bool IsCellphone(string objectName)
    {
        return objectName == "Celular" || objectName == "cellphone";
    }

    private bool CanTargetWithCurrentFilter(string objectName)
    {
        KeyCode requiredFilter = GetSelectionFilter(objectName);
        return requiredFilter == KeyCode.None ||
               (glassesFilter != null && glassesFilter.IsFilterActive(requiredFilter));
    }

    private void ClearTarget()
    {
        RestoreHighlight();
        currentTarget = null;
    }

    private void ApplyHighlight()
    {
        if (currentTarget == null || currentTarget.visualRoot == null)
            return;

        Renderer[] renderers = currentTarget.visualRoot.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            MaterialPropertyBlock original = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(original);
            originalBlocks[renderer] = original;

            highlightBlock.Clear();
            renderer.GetPropertyBlock(highlightBlock);
            highlightBlock.SetColor(baseColorId, highlightColor);
            highlightBlock.SetColor(colorId, highlightColor);
            highlightBlock.SetColor(emissionColorId, highlightColor * 0.35f);
            renderer.SetPropertyBlock(highlightBlock);
        }
    }

    private void RestoreHighlight()
    {
        foreach (KeyValuePair<Renderer, MaterialPropertyBlock> entry in originalBlocks)
        {
            if (entry.Key != null)
                entry.Key.SetPropertyBlock(entry.Value);
        }
        originalBlocks.Clear();
    }

    private void OnDisable()
    {
        ClearTarget();
    }

    private void OnGUI()
    {
        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            messageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                normal = { textColor = Color.white }
            };
            introStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        GUI.Label(new Rect(centerX - 5f, centerY - 10f, 10f, 20f), "+", messageStyle);

        if (currentTarget != null && !string.IsNullOrEmpty(currentTarget.prompt))
            GUI.Box(new Rect(centerX - 155f, centerY + 28f, 310f, 34f), currentTarget.prompt, promptStyle);

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            GUI.Box(new Rect(centerX - 310f, Screen.height - 120f, 620f, 76f), message, promptStyle);

        if (currentTarget != null && currentTarget.isOccluded && currentTarget.visualRoot != null)
        {
            Vector3 screenPosition = playerCamera.WorldToScreenPoint(currentTarget.visualRoot.position);
            if (screenPosition.z > 0f)
            {
                Rect marker = new Rect(screenPosition.x - 58f, Screen.height - screenPosition.y - 18f, 116f, 36f);
                GUI.Box(marker, "Celular", promptStyle);
            }
        }

        if (!string.IsNullOrEmpty(pendingTasksMessage) && Time.unscaledTime < pendingTasksUntil)
            GUI.Box(new Rect(centerX - 320f, centerY - 110f, 640f, 220f), pendingTasksMessage, introStyle);

        GUI.Label(new Rect(18f, 18f, 170f, 26f), "Tab - Inventario", messageStyle);
        if (inventoryOpen)
            GUI.Box(new Rect(18f, 44f, 240f, 150f), inventory.GetContentsText(), promptStyle);

        if (introVisible)
        {
            string intro = IntroText + "\n\nPulsa E o haz clic para continuar";
            GUI.Box(new Rect(Screen.width * 0.2f, Screen.height * 0.32f, Screen.width * 0.6f, 150f), intro, introStyle);
        }
    }
}
