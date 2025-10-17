using UnityEngine;
using UnityEngine.InputSystem;   // NEW input system
using Mirror;
using System.Collections.Generic;

public class RTSPlayer : NetworkBehaviour
{
    [Header("Raycast Masks")]
    public LayerMask groundMask;
    public LayerMask selectableMask;

    [Header("UI")]
    public RectTransform selectionBoxUI; // assign the SelectionBox Image here in Inspector

    Camera cam;

    Vector2 dragStart;
    bool dragging;

    readonly List<Selectable> currentSelection = new();

    void Start()
    {
        if (!isLocalPlayer) return;
        cam = Camera.main;
        if (selectionBoxUI != null) selectionBoxUI.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        HandleSelectionAndOrders();
    }

    void HandleSelectionAndOrders()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // --- begin drag on LMB down ---
        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            dragStart = mouse.position.ReadValue();

            if (selectionBoxUI != null)
            {
                selectionBoxUI.gameObject.SetActive(true);
                selectionBoxUI.sizeDelta = Vector2.zero;
                selectionBoxUI.anchoredPosition = dragStart;
                selectionBoxUI.pivot = new Vector2(0, 1);          // top-left
                selectionBoxUI.anchorMin = new Vector2(0, 1);
                selectionBoxUI.anchorMax = new Vector2(0, 1);
            }
        }

        // --- while dragging, update the rectangle ---
        if (dragging && selectionBoxUI != null)
        {
            Vector2 curr = mouse.position.ReadValue();

            float left = Mathf.Min(dragStart.x, curr.x);
            float right = Mathf.Max(dragStart.x, curr.x);
            float top = Mathf.Max(dragStart.y, curr.y);
            float bottom = Mathf.Min(dragStart.y, curr.y);

            selectionBoxUI.anchoredPosition = new Vector2(left, top);
            selectionBoxUI.sizeDelta = new Vector2(right - left, top - bottom);
        }

        // --- on LMB up: either select or issue move if single-clicked on ground ---
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (selectionBoxUI != null) selectionBoxUI.gameObject.SetActive(false);
            bool wasDragging = dragging;
            dragging = false;

            Vector2 end = mouse.position.ReadValue();
            bool isClick = (end - dragStart).sqrMagnitude < 25f;

            // keep previous selection to allow click-on-ground → move
            var prevSelection = new List<Selectable>(currentSelection);

            // clear old selection (we’ll repopulate)
            foreach (var s in currentSelection) s.SetSelected(false);
            currentSelection.Clear();

            if (isClick)
            {
                // if we clicked a selectable, select it; else if we clicked ground, issue move for previous selection
                Ray ray = cam.ScreenPointToRay(end);
                if (Physics.Raycast(ray, out var hit, 2000f, selectableMask))
                {
                    var sel = hit.collider.GetComponentInParent<Selectable>();
                    if (sel != null)
                    {
                        sel.SetSelected(true);
                        currentSelection.Add(sel);
                    }
                }
                else if (prevSelection.Count > 0 && Physics.Raycast(ray, out var groundHit, 2000f, groundMask))
                {
                    Vector3 dest = groundHit.point;
                    foreach (var s in prevSelection)
                    {
                        var unit = s.GetComponent<NetworkUnit>();
                        if (unit != null) unit.CmdMove(dest);  // LEFT CLICK TO MOVE
                    }
                    // restore selection visual on prevSelection (we “used” it)
                    foreach (var s in prevSelection) s.SetSelected(true);
                    currentSelection.AddRange(prevSelection);
                }
            }
            else // box select
            {
                Rect rect = GetScreenRect(dragStart, end);
                var all = GameObject.FindObjectsOfType<Selectable>(); // v0; optimize later
                foreach (var s in all)
                {
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

    static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 bl = new(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y));
        Vector2 tr = new(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        return Rect.MinMaxRect(bl.x, bl.y, tr.x, tr.y);
    }
}
