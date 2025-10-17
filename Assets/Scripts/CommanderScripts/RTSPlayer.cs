using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class RTSPlayer : NetworkBehaviour
{
    [Header("Raycast Masks")]
    [Tooltip("Layers the ground/terrain meshes are on (for right-click movement).")]
    public LayerMask groundMask;
    [Tooltip("Layers your unit colliders are on (for selection).")]
    public LayerMask selectableMask;

    [Header("Selection UI (optional)")]
    [Tooltip("Leave empty on the prefab; this script will auto-find an inactive object named or tagged 'SelectionBox'.")]
    public RectTransform selectionBoxUI;

    [Header("Tuning")]
    [Tooltip("Squared pixel distance to treat a press/release as a click instead of a drag.")]
    public float clickSqrThreshold = 25f;

    // Assigned by the server in CustomNetworkManager.OnServerAddPlayer
    [SyncVar] public ushort teamId;

    Camera cam;
    Vector2 dragStart;
    bool dragging;

    readonly List<Selectable> currentSelection = new();

    void Awake()
    {
        // If someone left it enabled in the editor, hide it at runtime.
        if (selectionBoxUI != null)
            selectionBoxUI.gameObject.SetActive(false);
    }

    void Start()
    {
        if (!isLocalPlayer) return;

        cam = Camera.main;

        // Robust auto-find that works even if the SelectionBox object is inactive.
        if (selectionBoxUI == null)
        {
#if UNITY_2022_3_OR_NEWER
            var rects = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var rects = Resources.FindObjectsOfTypeAll<RectTransform>(); // includes inactive, but also assets
#endif
            foreach (var r in rects)
            {
                if (!r || !r.gameObject.scene.IsValid()) continue; // ignore assets/prefabs
                if (r.name == "SelectionBox" || r.CompareTag("SelectionBox"))
                {
                    selectionBoxUI = r;
                    break;
                }
            }
            if (selectionBoxUI == null)
                Debug.LogWarning("[RTSPlayer] SelectionBox not found. Name it 'SelectionBox' or tag it 'SelectionBox'.");
        }

        if (selectionBoxUI != null)
            selectionBoxUI.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (selectionBoxUI != null)
            selectionBoxUI.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // If we're currently placing a building, ignore selection & move input.
        var placer = GetComponent<BuildingPlacer>();
        if (placer != null && placer.IsPlacing)
            return;

        HandleSelection();
        HandleRightClickMove();
    }

    // -------------------------
    // Selection (LMB + drag box)
    // -------------------------
    void HandleSelection()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // Begin drag
        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            dragStart = mouse.position.ReadValue();

            if (selectionBoxUI != null)
            {
                selectionBoxUI.gameObject.SetActive(true);

                // Bottom-left anchors so screen coordinates map directly.
                selectionBoxUI.pivot = new Vector2(0, 0);
                selectionBoxUI.anchorMin = new Vector2(0, 0);
                selectionBoxUI.anchorMax = new Vector2(0, 0);

                selectionBoxUI.anchoredPosition = dragStart;
                selectionBoxUI.sizeDelta = Vector2.zero;
            }
        }

        // While dragging, update the rectangle
        if (dragging && selectionBoxUI != null)
        {
            Vector2 curr = mouse.position.ReadValue();

            float left = Mathf.Min(dragStart.x, curr.x);
            float right = Mathf.Max(dragStart.x, curr.x);
            float bottom = Mathf.Min(dragStart.y, curr.y);
            float top = Mathf.Max(dragStart.y, curr.y);

            selectionBoxUI.anchoredPosition = new Vector2(left, bottom);
            selectionBoxUI.sizeDelta = new Vector2(right - left, top - bottom);
        }

        // Release → click or box select
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (selectionBoxUI != null)
                selectionBoxUI.gameObject.SetActive(false);

            dragging = false;

            Vector2 end = mouse.position.ReadValue();
            bool isClick = (end - dragStart).sqrMagnitude < clickSqrThreshold;

            // Clear previous selection visuals
            foreach (var s in currentSelection)
                s.SetSelected(false);
            currentSelection.Clear();

            if (isClick)
            {
                Ray ray = cam.ScreenPointToRay(end);
                if (Physics.Raycast(ray, out var hit, 2000f, selectableMask))
                {
                    var sel = hit.collider.GetComponentInParent<Selectable>();
                    if (sel != null)
                    {
                        var own = sel.GetComponent<TeamOwnership>();
                        if (own != null && own.teamId == teamId)
                        {
                            sel.SetSelected(true);
                            currentSelection.Add(sel);
                        }
                    }
                }
            }
            else
            {
                Rect rect = GetScreenRect(dragStart, end);
                var all = GameObject.FindObjectsOfType<Selectable>(); // v0: simple; replace with manager/pooling later
                foreach (var s in all)
                {
                    var own = s.GetComponent<TeamOwnership>();
                    if (own == null || own.teamId != teamId) continue;

                    Vector3 sp = cam.WorldToScreenPoint(s.transform.position);
                    if (sp.z > 0 && rect.Contains(sp))
                    {
                        s.SetSelected(true);
                        currentSelection.Add(s);
                    }
                }
            }
        }
    }

    // -------------------------
    // Orders (RMB to move)
    // -------------------------
    void HandleRightClickMove()
    {
        if (currentSelection.Count == 0) return;

        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var groundHit, 2000f, groundMask))
            {
                Vector3 dest = groundHit.point;

                for (int i = 0; i < currentSelection.Count; i++)
                {
                    var unit = currentSelection[i].GetComponent<NetworkUnit>();
                    if (unit != null)
                        unit.CmdMove(dest); // server validates team
                }
            }
        }
    }

    // Helper: screen-space rect from two corners
    static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 bl = new(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y));
        Vector2 tr = new(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        return Rect.MinMaxRect(bl.x, bl.y, tr.x, tr.y);
    }
}
