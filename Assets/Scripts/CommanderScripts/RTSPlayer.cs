using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class RTSPlayer : NetworkBehaviour
{
    [Header("Raycast Masks")]
    public LayerMask groundMask;
    public LayerMask selectableMask;

    [Header("Selection UI")]
    public RectTransform selectionBoxUI;

    [Header("Selection Settings")]
    public float clickSqrThreshold = 25f;
    public float doubleClickTime = 0.25f;

    [SyncVar] public ushort teamId;

    Camera cam;
    Vector2 dragStart;
    bool dragging;
    float lastClickTime;
    Selectable lastClickedType;

    readonly List<Selectable> currentSelection = new();

    void Awake()
    {
        if (selectionBoxUI != null)
            selectionBoxUI.gameObject.SetActive(false);
    }

    void Start()
    {
        if (!isLocalPlayer) return;
        cam = Camera.main;

#if UNITY_2022_3_OR_NEWER
        if (selectionBoxUI == null)
        {
            var rects = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in rects)
                if (r && r.gameObject.scene.IsValid() && (r.name == "SelectionBox" || r.CompareTag("SelectionBox")))
                { selectionBoxUI = r; break; }
        }
#endif
        if (selectionBoxUI) selectionBoxUI.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // Pause selection while any placer is active
        var placer = GetComponent<BuildingPlacer>();
        if (placer != null && placer.IsPlacing)
            return;

        HandleSelection();
        HandleRightClickMove();
    }

    // ================= Selection =================
    void HandleSelection()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null || cam == null) return;

        bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);

        // start drag
        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            dragStart = mouse.position.ReadValue();

            if (selectionBoxUI)
            {
                selectionBoxUI.gameObject.SetActive(true);
                selectionBoxUI.pivot = new Vector2(0, 0);
                selectionBoxUI.anchorMin = new Vector2(0, 0);
                selectionBoxUI.anchorMax = new Vector2(0, 0);
                selectionBoxUI.anchoredPosition = dragStart;
                selectionBoxUI.sizeDelta = Vector2.zero;
            }
        }

        // update box
        if (dragging && selectionBoxUI)
        {
            Vector2 curr = mouse.position.ReadValue();
            float left = Mathf.Min(dragStart.x, curr.x);
            float right = Mathf.Max(dragStart.x, curr.x);
            float bottom = Mathf.Min(dragStart.y, curr.y);
            float top = Mathf.Max(dragStart.y, curr.y);
            selectionBoxUI.anchoredPosition = new Vector2(left, bottom);
            selectionBoxUI.sizeDelta = new Vector2(right - left, top - bottom);
        }

        // release
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (selectionBoxUI) selectionBoxUI.gameObject.SetActive(false);
            dragging = false;

            Vector2 end = mouse.position.ReadValue();
            bool isClick = (end - dragStart).sqrMagnitude < clickSqrThreshold;

            if (!shift && !ctrl)
                ClearSelection();

            if (isClick)
            {
                Ray ray = cam.ScreenPointToRay(end);
                if (Physics.Raycast(ray, out var hit, 2000f, selectableMask))
                {
                    var sel = hit.collider.GetComponentInParent<Selectable>();
                    if (sel != null && BelongsToMe(sel))
                    {
                        bool isDouble = (Time.time - lastClickTime) <= doubleClickTime && lastClickedType != null &&
                                        sel.GetType() == lastClickedType.GetType();
                        lastClickTime = Time.time;
                        lastClickedType = sel;

                        if (ctrl)
                        {
                            // toggle
                            if (currentSelection.Contains(sel)) { sel.SetSelected(false); currentSelection.Remove(sel); }
                            else { sel.SetSelected(true); currentSelection.Add(sel); }
                        }
                        else
                        {
                            sel.SetSelected(true);
                            if (!currentSelection.Contains(sel)) currentSelection.Add(sel);

                            if (isDouble)
                                SelectAllOfSameTypeOnScreen(sel);
                        }
                    }
                }
            }
            else
            {
                Rect rect = GetScreenRect(dragStart, end);
                var all = GameObject.FindObjectsOfType<Selectable>();
                foreach (var s in all)
                {
                    if (!BelongsToMe(s)) continue;
                    Vector3 sp = cam.WorldToScreenPoint(s.transform.position);
                    if (sp.z > 0 && rect.Contains(sp))
                    {
                        if (ctrl && currentSelection.Contains(s))
                        { s.SetSelected(false); currentSelection.Remove(s); }
                        else
                        { s.SetSelected(true); if (!currentSelection.Contains(s)) currentSelection.Add(s); }
                    }
                }
            }
        }
    }

    void ClearSelection()
    {
        foreach (var s in currentSelection) s.SetSelected(false);
        currentSelection.Clear();
    }

    bool BelongsToMe(Selectable s)
    {
        var own = s.GetComponent<TeamOwnership>();
        return own != null && own.teamId == teamId;
    }

    void SelectAllOfSameTypeOnScreen(Selectable sample)
    {
        var type = sample.GetType();
        var all = GameObject.FindObjectsOfType<Selectable>();
        foreach (var s in all)
        {
            if (!BelongsToMe(s)) continue;
            if (s.GetType() != type) continue;
            Vector3 sp = cam.WorldToScreenPoint(s.transform.position);
            if (sp.z > 0 && sp.x >= 0 && sp.x <= Screen.width && sp.y >= 0 && sp.y <= Screen.height)
            {
                s.SetSelected(true);
                if (!currentSelection.Contains(s)) currentSelection.Add(s);
            }
        }
    }

    static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 bl = new(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y));
        Vector2 tr = new(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        return Rect.MinMaxRect(bl.x, bl.y, tr.x, tr.y);
    }

    // ================= Orders: Right-click move with formation =================
    void HandleRightClickMove()
    {
        if (currentSelection.Count == 0) return;
        var mouse = Mouse.current; if (mouse == null) return;
        if (!mouse.rightButton.wasPressedThisFrame) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var groundHit, 2000f, groundMask)) return;

        Vector3 dest = groundHit.point;

        // fan-out slots
        var slots = ZeroPoint.Orders.FormationPlanner.GridSlots(dest, currentSelection.Count, 1.5f);

        for (int i = 0; i < currentSelection.Count; i++)
        {
            var unit = currentSelection[i].GetComponent<NetworkUnit>();
            if (unit != null)
                unit.CmdMove(slots[i]); // server validates team
        }
    }
}
