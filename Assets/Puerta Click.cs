using System.Collections.Generic;
using UnityEngine;

public class Interaction : MonoBehaviour
{
    public float distance = 3f;
    public KeyCode interactionKey = KeyCode.E;
    public Color highlightColor = new Color(0.35f, 0.9f, 1f, 1f);
    public AudioClip speakerMusic;

    private Camera playerCamera;
    private PlayerMovement playerMovement;
    private InventarioJugador inventory;
    private InteractableTarget currentTarget;
    private string message;
    private float messageUntil;
    private bool introVisible = true;
    private float introUntil;
    private bool inventoryOpen;
    private int activeInventoryTab;
    private string selectedInventoryItemId;
    private string selectedArchiveMessage;
    private readonly List<string> messageArchive = new List<string>();
    private bool papersRead;
    private bool televisionOn = true;
    private bool speakersPlaying = true;
    private GameObject televisionScreen;
    private string pendingTasksMessage;
    private float pendingTasksUntil;
    private GUIStyle promptStyle;
    private GUIStyle messageStyle;
    private GUIStyle introStyle;
    private readonly Dictionary<Renderer, MaterialPropertyBlock> originalBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
    private readonly List<Transform> highlightOnlyTargets = new List<Transform>();
    private readonly List<FilterHiddenTarget> filterHiddenTargets = new List<FilterHiddenTarget>();
    private readonly List<AudioSource> speakerAudioSources = new List<AudioSource>();
    private MaterialPropertyBlock highlightBlock;
    private MaterialPropertyBlock filterGlowBlock;
    private GlassesFilter glassesFilter;
    private GUIStyle inventoryTitleStyle;
    private GUIStyle inventoryDescriptionStyle;
    private GUIStyle inventoryButtonStyle;
    private GUIStyle keyHaloStyle;
    private Vector2 inventoryScroll;
    private Vector2 archiveScroll;
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
        public bool hideColliders;
        public Renderer[] renderers;
        public bool[] rendererWasEnabled;
        public MaterialPropertyBlock[] originalMaterialBlocks;
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

    public bool IsInventoryOpen => inventoryOpen;

    private void Awake()
    {
        highlightBlock = new MaterialPropertyBlock();
        filterGlowBlock = new MaterialPropertyBlock();
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

        playerMovement = GetComponent<PlayerMovement>();
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
            playerCamera = Camera.main;
        glassesFilter = GetComponent<GlassesFilter>();

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        HashSet<int> processedSpeakers = new HashSet<int>();
        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (namedObjectPrompts.ContainsKey(sceneTransform.name))
                highlightOnlyTargets.Add(sceneTransform);

            if (IsTelevision(sceneTransform.name))
                InitializeTelevision(sceneTransform);

            if (IsSpeaker(sceneTransform.name) && processedSpeakers.Add(sceneTransform.GetInstanceID()))
                InitializeSpeaker(sceneTransform);

            KeyCode requiredFilter = GetVisibilityFilter(sceneTransform.name);
            if (requiredFilter == KeyCode.None)
                continue;

            Renderer[] renderers = sceneTransform.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = sceneTransform.GetComponentsInChildren<Collider>(true);
            FilterHiddenTarget hiddenTarget = new FilterHiddenTarget
            {
                root = sceneTransform,
                requiredFilter = requiredFilter,
                hideColliders = GetRequiredFilter(sceneTransform.name) != KeyCode.None,
                renderers = renderers,
                rendererWasEnabled = GetRendererEnabledStates(renderers),
                originalMaterialBlocks = GetOriginalMaterialBlocks(renderers),
                colliders = colliders,
                colliderWasEnabled = GetColliderEnabledStates(colliders),
                isVisible = true
            };
            filterHiddenTargets.Add(hiddenTarget);
        }

        SetSpeakersPlaying(true);
    }

    private void InitializeTelevision(Transform television)
    {
        Transform[] children = television.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == "Screen")
            {
                televisionScreen = child.gameObject;
                televisionScreen.SetActive(true);
                televisionOn = true;
                return;
            }
        }
    }

    private void InitializeSpeaker(Transform speaker)
    {
        AudioSource[] sources = speaker.GetComponentsInChildren<AudioSource>(true);
        if (sources.Length == 0)
            sources = new[] { speaker.gameObject.AddComponent<AudioSource>() };

        foreach (AudioSource source in sources)
        {
            if (source == null)
                continue;

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0.35f;
            source.volume = 0.25f;
            source.minDistance = 2f;
            source.maxDistance = 12f;
            if (speakerMusic != null)
                source.clip = speakerMusic;

            speakerAudioSources.Add(source);
        }
    }

    private void SetSpeakersPlaying(bool shouldPlay)
    {
        speakersPlaying = shouldPlay;
        foreach (AudioSource source in speakerAudioSources)
        {
            if (source == null || source.clip == null)
                continue;

            if (shouldPlay)
            {
                if (!source.isPlaying)
                    source.Play();
            }
            else if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }

    private bool IsTelevision(string objectName)
    {
        return objectName == "Televisor" || objectName == "TV_Apt_01";
    }

    private bool IsSpeaker(string objectName)
    {
        return objectName == "Parlante_Izquierdo" || objectName == "Parlante_Derecho" ||
               objectName == "SS_Stand_01 (1)" || objectName == "SS_Stand_01 (2)" ||
               objectName == "SS_Stand_01 (3)" || objectName == "SS_Stand_01 (4)";
    }

    private string GetNamedObjectPrompt(Transform target)
    {
        if (IsTelevision(target.name))
        {
            if (!inventory.HasItem(InventarioJugador.CelularId))
                return "E - Necesitas el celular";

            return televisionOn ? "E - Apagar televisión" : "E - Prender televisión";
        }

        if (IsSpeaker(target.name))
            return speakersPlaying ? "E - Apagar parlantes" : "E - Prender parlantes";

        return namedObjectPrompts[target.name];
    }

    private void SetInventoryOpen(bool open)
    {
        inventoryOpen = open;
        if (playerMovement != null)
            playerMovement.enabled = !open;

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    private void Start()
    {
        introUntil = Time.unscaledTime + 10f;
        AddToArchive(IntroText);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (inventoryOpen)
                SetInventoryOpen(false);

            introVisible = false;
            message = null;
            pendingTasksMessage = null;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
            SetInventoryOpen(!inventoryOpen);

        if (inventoryOpen)
            return;

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
        else if (currentTarget != null && nextTarget != null)
        {
            currentTarget.prompt = nextTarget.prompt;
            currentTarget.canInteract = nextTarget.canInteract;
            currentTarget.isOccluded = nextTarget.isOccluded;
        }

        bool pressedInteract = Input.GetKeyDown(interactionKey) || Input.GetMouseButtonDown(0);
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
            return MakeTarget(namedObject, namedObject, GetNamedObjectPrompt(namedObject));

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
            : GetNamedObjectPrompt(closestTarget);
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
            if (IsTelevision(target.name))
            {
                if (!inventory.HasItem(InventarioJugador.CelularId))
                {
                    ShowMessage("Necesitas el celular para apagar la televisión");
                    return;
                }

                televisionOn = !televisionOn;
                if (televisionScreen != null)
                    televisionScreen.SetActive(televisionOn);
                ShowMessage(televisionOn ? "Televisión encendida" : "Televisión apagada");
            }
            else if (IsSpeaker(target.name))
            {
                SetSpeakersPlaying(!speakersPlaying);
                ShowMessage(speakersPlaying ? "Parlantes encendidos" : "Parlantes apagados");
            }
            else if (target.name == "Papers")
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
        if (televisionOn)
            pending.Add("Apagar la televisión con el celular");
        if (speakersPlaying)
            pending.Add("Apagar los parlantes");
        if (AnyControlledLightsAreOn())
            pending.Add("Apagar las luces");

        if (pending.Count > 0)
        {
            pendingTasksMessage = "Antes de dormir, termina estos pendientes:\n\n• " + string.Join("\n• ", pending);
            pendingTasksUntil = Time.unscaledTime + 12f;
            AddToArchive(pendingTasksMessage);
            return;
        }

        bed.Dormir();
        ShowMessage("Completaste los pendientes. Buen descanso.", 3f);
    }

    private bool AnyControlledLightsAreOn()
    {
        InterruptorLuz[] switches = FindObjectsByType<InterruptorLuz>(FindObjectsSortMode.None);
        foreach (InterruptorLuz lightSwitch in switches)
        {
            if (lightSwitch != null && lightSwitch.IsOn)
                return true;
        }

        return false;
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
        AddToArchive(text);
    }

    private void AddToArchive(string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && !messageArchive.Contains(text))
            messageArchive.Add(text);
    }

    private void UpdateFilterHiddenTargets()
    {
        foreach (FilterHiddenTarget target in filterHiddenTargets)
        {
            if (target.root == null || !target.root.gameObject.activeInHierarchy)
                continue;

            bool shouldBeVisible = glassesFilter != null &&
                                   glassesFilter.IsFilterActive(target.requiredFilter);
            if (target.isVisible != shouldBeVisible)
            {
                for (int i = 0; i < target.renderers.Length; i++)
                {
                    if (target.renderers[i] != null)
                    {
                        target.renderers[i].enabled = shouldBeVisible && target.rendererWasEnabled[i];
                        if (!shouldBeVisible && target.originalMaterialBlocks[i] != null)
                            target.renderers[i].SetPropertyBlock(target.originalMaterialBlocks[i]);
                    }
                }

                if (target.hideColliders)
                {
                    for (int i = 0; i < target.colliders.Length; i++)
                    {
                        if (target.colliders[i] != null)
                            target.colliders[i].enabled = shouldBeVisible && target.colliderWasEnabled[i];
                    }
                }

                target.isVisible = shouldBeVisible;
            }

            if (shouldBeVisible && target.hideColliders)
            {
                ApplyKeyGlow(target);
            }
        }
    }

    private void ApplyKeyGlow(FilterHiddenTarget target)
    {
        float pulse = (Mathf.Sin(Time.unscaledTime * 4.5f) + 1f) * 0.5f;
        Color glowColor = Color.Lerp(new Color(1f, 0.78f, 0.46f, 1f), Color.white, pulse);
        float emission = 0.8f + pulse * 1.2f;

        foreach (Renderer renderer in target.renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            filterGlowBlock.Clear();
            renderer.GetPropertyBlock(filterGlowBlock);
            filterGlowBlock.SetColor(baseColorId, glowColor);
            filterGlowBlock.SetColor(colorId, glowColor);
            filterGlowBlock.SetColor(emissionColorId, Color.white * emission);
            renderer.SetPropertyBlock(filterGlowBlock);
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

    private MaterialPropertyBlock[] GetOriginalMaterialBlocks(Renderer[] renderers)
    {
        MaterialPropertyBlock[] blocks = new MaterialPropertyBlock[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            blocks[i] = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(blocks[i]);
        }

        return blocks;
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

    private KeyCode GetVisibilityFilter(string objectName)
    {
        KeyCode itemFilter = GetRequiredFilter(objectName);
        if (itemFilter != KeyCode.None)
            return itemFilter;

        return IsCellphone(objectName) ? KeyCode.C : KeyCode.None;
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
            inventoryTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            inventoryDescriptionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 18,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            inventoryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 16,
                wordWrap = true
            };
            keyHaloStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        if (inventoryOpen)
        {
            DrawInventoryPanel();
            return;
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        GUI.Label(new Rect(centerX - 5f, centerY - 10f, 10f, 20f), "+", messageStyle);

        if (currentTarget != null && !string.IsNullOrEmpty(currentTarget.prompt))
            GUI.Box(new Rect(centerX - 155f, centerY + 28f, 310f, 34f), currentTarget.prompt, promptStyle);

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
        {
            GUI.Box(new Rect(centerX - 310f, Screen.height - 120f, 620f, 76f), message, promptStyle);
            GUI.Label(new Rect(centerX + 170f, Screen.height - 48f, 130f, 22f), "Esc para cerrar", messageStyle);
        }

        if (currentTarget != null && currentTarget.isOccluded && currentTarget.visualRoot != null)
        {
            Vector3 screenPosition = playerCamera.WorldToScreenPoint(currentTarget.visualRoot.position);
            if (screenPosition.z > 0f)
            {
                Rect marker = new Rect(screenPosition.x - 58f, Screen.height - screenPosition.y - 18f, 116f, 36f);
                GUI.Box(marker, "Celular", promptStyle);
            }
        }

        DrawRevealedKeyHalos();

        if (!string.IsNullOrEmpty(pendingTasksMessage) && Time.unscaledTime < pendingTasksUntil)
        {
            GUI.Box(new Rect(centerX - 320f, centerY - 160f, 640f, 320f), pendingTasksMessage, introStyle);
            GUI.Label(new Rect(centerX + 160f, centerY + 126f, 130f, 22f), "Esc para cerrar", messageStyle);
        }

        GUI.Label(new Rect(18f, 18f, 170f, 26f), "Tab - Inventario", messageStyle);

        if (introVisible)
        {
            string intro = IntroText + "\n\nPulsa E, haz clic o Esc para cerrar";
            GUI.Box(new Rect(Screen.width * 0.2f, Screen.height * 0.32f, Screen.width * 0.6f, 150f), intro, introStyle);
        }
    }

    private void DrawRevealedKeyHalos()
    {
        if (playerCamera == null)
            return;

        foreach (FilterHiddenTarget target in filterHiddenTargets)
        {
            if (target.root == null || !target.isVisible || !target.hideColliders)
                continue;

            Vector3 screenPosition = playerCamera.WorldToScreenPoint(target.root.position);
            if (screenPosition.z <= 0f || screenPosition.x < 0f || screenPosition.x > Screen.width ||
                screenPosition.y < 0f || screenPosition.y > Screen.height)
            {
                continue;
            }

            GUI.Label(new Rect(screenPosition.x - 24f, Screen.height - screenPosition.y - 24f, 48f, 48f),
                "◉", keyHaloStyle);
        }
    }

    private void DrawInventoryPanel()
    {
        float panelWidth = Mathf.Min(760f, Screen.width - 40f);
        float panelHeight = Mathf.Min(470f, Screen.height - 40f);
        float left = (Screen.width - panelWidth) * 0.5f;
        float top = (Screen.height - panelHeight) * 0.5f;
        Rect panel = new Rect(left, top, panelWidth, panelHeight);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(left + 20f, top + 12f, panelWidth - 150f, 42f), "INVENTARIO", inventoryTitleStyle);

        if (GUI.Button(new Rect(left + panelWidth - 135f, top + 20f, 115f, 34f), "Cerrar (Tab)"))
        {
            SetInventoryOpen(false);
            return;
        }

        if (GUI.Button(new Rect(left + 24f, top + 58f, 150f, 34f), "Inventario"))
            activeInventoryTab = 0;
        if (GUI.Button(new Rect(left + 184f, top + 58f, 150f, 34f), "Archivos"))
            activeInventoryTab = 1;

        float listLeft = left + 24f;
        float listTop = top + 108f;
        float listWidth = Mathf.Min(270f, panelWidth * 0.42f);
        float listHeight = panelHeight - 142f;
        float descriptionLeft = left + listWidth + 54f;
        float descriptionWidth = panelWidth - listWidth - 78f;
        string description;

        if (activeInventoryTab == 0)
        {
            GUI.Label(new Rect(listLeft, listTop, listWidth, 28f), "Objetos", messageStyle);
            IReadOnlyList<string> itemIds = inventory.ItemIds;
            if (itemIds.Count == 0)
            {
                GUI.Label(new Rect(listLeft, listTop + 36f, listWidth, 40f), "Inventario vacío", inventoryDescriptionStyle);
            }
            else
            {
                float contentHeight = itemIds.Count * 48f;
                Rect scrollRect = new Rect(listLeft, listTop + 30f, listWidth, listHeight - 30f);
                Rect contentRect = new Rect(0f, 0f, listWidth - 20f, contentHeight);
                inventoryScroll = GUI.BeginScrollView(scrollRect, inventoryScroll, contentRect);
                for (int i = 0; i < itemIds.Count; i++)
                {
                    string itemId = itemIds[i];
                    Rect buttonRect = new Rect(0f, i * 48f, listWidth - 24f, 42f);
                    if (GUI.Button(buttonRect, inventory.GetDisplayName(itemId), inventoryButtonStyle))
                        selectedInventoryItemId = itemId;
                }
                GUI.EndScrollView();
            }

            description = string.IsNullOrEmpty(selectedInventoryItemId)
                ? "Selecciona un objeto para ver su descripción."
                : inventory.GetDescription(selectedInventoryItemId);
        }
        else
        {
            GUI.Label(new Rect(listLeft, listTop, listWidth, 28f), "Mensajes", messageStyle);
            if (messageArchive.Count == 0)
            {
                GUI.Label(new Rect(listLeft, listTop + 36f, listWidth, 40f), "Todavía no hay mensajes guardados.", inventoryDescriptionStyle);
            }
            else
            {
                float contentHeight = messageArchive.Count * 54f;
                Rect scrollRect = new Rect(listLeft, listTop + 30f, listWidth, listHeight - 30f);
                Rect contentRect = new Rect(0f, 0f, listWidth - 20f, contentHeight);
                archiveScroll = GUI.BeginScrollView(scrollRect, archiveScroll, contentRect);
                for (int i = 0; i < messageArchive.Count; i++)
                {
                    string entry = messageArchive[i];
                    string buttonText = (i + 1) + ". " + Shorten(entry, 42);
                    Rect buttonRect = new Rect(0f, i * 54f, listWidth - 24f, 48f);
                    if (GUI.Button(buttonRect, buttonText, inventoryButtonStyle))
                        selectedArchiveMessage = entry;
                }
                GUI.EndScrollView();
            }

            description = string.IsNullOrEmpty(selectedArchiveMessage)
                ? "Selecciona un mensaje para volver a leerlo."
                : selectedArchiveMessage;
        }

        GUI.Label(new Rect(descriptionLeft, listTop, descriptionWidth, 28f), "Descripción", messageStyle);
        GUI.Box(new Rect(descriptionLeft, listTop + 34f, descriptionWidth, listHeight - 34f), GUIContent.none);
        GUI.Label(new Rect(descriptionLeft + 16f, listTop + 50f, descriptionWidth - 32f, listHeight - 66f),
            description, inventoryDescriptionStyle);
    }

    private string Shorten(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }
}
