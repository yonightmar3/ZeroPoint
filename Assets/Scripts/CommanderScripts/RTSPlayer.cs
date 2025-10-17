using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using System.Collections.Generic;

public class RTSPlayer : NetworkBehaviour
{
    [Header("Raycast Masks")]
    public LayerMask groundMask;
    public LayerMask selectableMask;

    [Header("UI")]
    public RectTransform selectionBoxUI; // leave null on prefab; auto-find by name/tag

    Camera cam;
    Vector2 dragStart;
    bool dragging;

    readonly List<Selectable> currentSelection = new();

    void Awake()
    {
        if (selectionBoxUI != null) selectionBoxUI.gameObject.SetActive(false);
    }

    void Start()
    {
        if (!isLocalPlayer) return;

        cam = Camera.main;

        if (selectionBoxUI == null)
        {
            var go = GameObject.Find("SelectionBox");
            if (go == null) go = GameObject.FindWithTag("SelectionBox");
            if (go != null) selectionBoxUI = go.GetComponent<RectTransform>();
        }
        if (selectionBoxUI != null) selectionBoxUI.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (selectionBoxUI != null) selectionBoxUI.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        HandleSelection();
        HandleRightClickMove();
    }

    void HandleSelection()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // LMB down → start drag box
        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            dragStart = mouse.position.ReadValue();

            if (selectionBoxUI != null)
            {
                selectionBoxUI.gameObject.SetActive(true);

                // BOTTOM-LEFT anchors/pivot so screen coords map directly
                selectionBoxUI.pivot = new Vector2(0, 0);
                selectionBoxUI.anchorMin = new Vector2(0, 0);
                selectionBoxUI.anchorMax = new Vector2(0, 0);

                selectionBoxUI.anchoredPosition = dragStart;
                selectionBoxUI.sizeDelta = Vector2.zero;
            }
        }

        // update drag box
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

        // LMB up → click or box-select
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (selectionBoxUI != null) selectionBoxUI.gameObject.SetActive(false);
            dragging = false;

            Vector2 end = mouse.position.ReadValue();
            bool isClick = (end - dragStart).sqrMagnitude < 25f;

            foreach (var s in currentSelection) s.SetSelected(false);
            currentSelection.Clear();

            if (isClick)
            {
                Ray ray = cam.ScreenPointToRay(end);
                if (Physics.Raycast(ray, out var hit, 2000f, selectableMask))
                {
                    var sel = hit.collider.GetComponentInParent<Selectable>();
                    if (sel != null) { sel.SetSelected(true); currentSelection.Add(sel); }
                }
            }
            else
            {
                Rect rect = GetScreenRect(dragStart, end);
                var all = GameObject.FindObjectsOfType<Selectable>(); // v0
                foreach (var s in all)
                {
                    Vector3 sp = cam.WorldToScreenPoint(s.transform.position);
                    if (sp.z > 0 && rect.Contains(sp))
                    { s.SetSelected(true); currentSelection.Add(s); }
                }
            }
        }
    }

    void HandleRightClickMove()
    {
        if (currentSelection.Count == 0) return;
        var mouse = Mouse.current; if (mouse == null || cam == null) return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var groundHit, 2000f, groundMask))
            {
                Vector3 dest = groundHit.point;
                foreach (var s in currentSelection)
                {
                    var unit = s.GetComponent<NetworkUnit>();
                    if (unit != null) unit.CmdMove(dest);
                }
            }
        }
    }

    static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 bl = new(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y));
        Vector2 tr = new(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        return Rect.MinMaxRect(bl.x, bl.y, tr.x, tr.y);
    }
}
