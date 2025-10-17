using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using System.Collections.Generic;

public class BuildingPlacer : NetworkBehaviour
{
    [Header("Prefabs (MUST be in NetworkManager → Registered Spawnable Prefabs)")]
    public GameObject player1BasePrefab;
    public GameObject player2BasePrefab;

    [Header("Placement")]
    public LayerMask groundMask;          // include your Ground layer
    public LayerMask buildingMask;        // include your Buildings layer (for overlap check)
    public float cellSize = 2f;           // grid size (meters)
    public float rotateStep = 90f;        // R to rotate
    public float gridHalfCells = 16;      // grid radius (in cells)
    public float gridYOffset = 0.01f;     // grid sits a hair above ground to avoid z-fighting

    Camera cam;
    RTSPlayer rtsPlayer;

    GameObject ghost;
    Quaternion ghostRot = Quaternion.identity;

    // Grid (only shown while placing)
    GameObject gridGO;
    readonly List<LineRenderer> gridLines = new();

    bool placing;
    bool showedMissWarning;
    bool hasPlacedBase;                   // TEMP rule: only one base per player

    float lastGroundY;                    // used to pin grid to ground

    public bool IsPlacing => placing;

    void Start()
    {
        if (!isLocalPlayer) return;

        cam = Camera.main;
        rtsPlayer = GetComponent<RTSPlayer>();

        Debug.Log("[BuildingPlacer] Ready. B=toggle, R=rotate, LMB=confirm, RMB/Esc=cancel.");

        if (cam == null) Debug.LogWarning("[BuildingPlacer] No Camera.main (tag your camera MainCamera).");
        if (player1BasePrefab == null || player2BasePrefab == null)
            Debug.LogWarning("[BuildingPlacer] Assign both team base prefabs.");
    }

    void OnDisable()
    {
        if (!isLocalPlayer) return;
        if (placing) { placing = false; DestroyGhost(); ShowGrid(false); }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // --- Toggle placement (B) ---
        if (kb.bKey.wasPressedThisFrame)
        {
            if (hasPlacedBase)
            {
                Debug.Log("[BuildingPlacer] You have the max bases placed (temp rule).");
            }
            else
            {
                placing = !placing;
                Debug.Log($"[BuildingPlacer] Toggle placing: {(placing ? "ON" : "OFF")}");
                if (placing) { CreateGhost(); ShowGrid(true); }
                else { DestroyGhost(); ShowGrid(false); }
            }
        }

        if (!placing) return;

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[BuildingPlacer] No camera available while placing."); return; }
        }

        if (ghost == null)
        {
            CreateGhost();
            if (ghost == null) return;
        }

        // --- Move ghost with raycast + grid snap ---
        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 5000f, groundMask))
        {
            showedMissWarning = false;

            Vector3 p = hit.point;
            float gx = Mathf.Round(p.x / cellSize) * cellSize;
            float gz = Mathf.Round(p.z / cellSize) * cellSize;

            // level to ground using renderer bounds so the bottom sits on the ground
            float halfHeight = GetGhostHalfHeight();
            float gy = hit.point.y + halfHeight;

            lastGroundY = hit.point.y; // <— store ground Y for grid

            Vector3 snapped = new Vector3(gx, gy, gz);
            ghost.transform.SetPositionAndRotation(snapped, ghostRot);

            UpdateGrid(snapped, lastGroundY);
            TintGhost(IsPlacementValid(snapped, ghostRot) ? 0.35f : 0.15f, IsPlacementValid(snapped, ghostRot) ? Color.white : new Color(1f, 0.3f, 0.3f));
        }
        else if (!showedMissWarning)
        {
            showedMissWarning = true;
            Debug.LogWarning("[BuildingPlacer] Ground raycast missed. Check groundMask & ground layer.");
        }

        // --- Rotate (R) ---
        if (kb.rKey.wasPressedThisFrame)
        {
            ghostRot *= Quaternion.Euler(0, rotateStep, 0);
            ghost.transform.rotation = ghostRot;
        }

        // --- Confirm once (LMB) ---
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector3 pos = ghost.transform.position;
            Quaternion rot = ghost.transform.rotation;

            // client-side overlap guard (server re-validates)
            if (!IsPlacementValid(pos, rot))
            {
                Debug.Log("[BuildingPlacer] Can't place here (overlaps a building).");
                return;
            }

            Debug.Log($"[BuildingPlacer] Confirm at {pos} rot {rot.eulerAngles}");
            CmdPlaceBase(pos, rot);

            // single placement: exit mode & lock further placements (temp rule)
            hasPlacedBase = true;
            placing = false;
            DestroyGhost();
            ShowGrid(false);
        }

        // --- Cancel (RMB or Esc) ---
        if (mouse.rightButton.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("[BuildingPlacer] Cancel placement.");
            placing = false;
            DestroyGhost();
            ShowGrid(false);
        }
    }

    // ===== Ghost helpers =====
    void CreateGhost()
    {
        DestroyGhost();
        GameObject src = (rtsPlayer != null && rtsPlayer.teamId == 2) ? player2BasePrefab : player1BasePrefab;
        if (src == null) { Debug.LogWarning("[BuildingPlacer] Team base prefab not assigned."); return; }

        ghost = Instantiate(src);
        ghostRot = Quaternion.Euler(0, Mathf.Round(ghost.transform.eulerAngles.y / rotateStep) * rotateStep, 0);
        ghost.transform.rotation = ghostRot;

        // disable colliders (so we don't self-overlap) and make transparent
        foreach (var c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
        TintGhost(0.35f, Color.white);

        Debug.Log($"[BuildingPlacer] Created ghost from '{src.name}' for team {(rtsPlayer ? rtsPlayer.teamId : (ushort)0)}");
    }

    void DestroyGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
    }

    float GetGhostHalfHeight()
    {
        var rends = ghost.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return 0f;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b.extents.y;
    }

    Bounds GetGhostBoundsWorld(Vector3 pos, Quaternion rot)
    {
        var rends = ghost.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(pos, Vector3.one * cellSize);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        // Move bounds to proposed pos/rot
        // approximate: use extents in local X/Z, height in Y (good enough for boxes)
        Vector3 size = b.size;
        return new Bounds(pos, size);
    }

    bool IsPlacementValid(Vector3 pos, Quaternion rot)
    {
        // Check overlap with existing buildings (on buildingMask) using an oriented box
        Bounds bw = GetGhostBoundsWorld(pos, rot);
        Vector3 halfExtents = bw.extents * 0.98f; // small shrink to avoid precision issues

        Collider[] hits = Physics.OverlapBox(
            bw.center,
            halfExtents,
            rot,
            buildingMask,
            QueryTriggerInteraction.Ignore
        );
        return hits.Length == 0;
    }

    void TintGhost(float alpha, Color tint)
    {
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                MakeMaterialTransparent(m, alpha, tint);
            }
            r.materials = mats;
        }
    }

    static void MakeMaterialTransparent(Material m, float alpha, Color tint)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // URP Lit: Transparent
#if UNITY_6000_0_OR_NEWER || UNITY_2022_3_OR_NEWER
        if (m.HasProperty("_BaseColor"))
        {
            var c = m.GetColor("_BaseColor");
            c = new Color(tint.r, tint.g, tint.b, alpha);
            m.SetColor("_BaseColor", c);
        }
        else
#endif
        {
            var c = new Color(tint.r, tint.g, tint.b, alpha);
            m.color = c;
        }
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    // ===== Grid helpers =====
    void ShowGrid(bool show)
    {
        if (show)
        {
            if (gridGO == null)
            {
                gridGO = new GameObject("PlacementGrid");
                int lines = Mathf.RoundToInt(gridHalfCells * 2f) + 1;
                for (int i = 0; i < lines * 2; i++)
                {
                    var lr = new GameObject($"grid_line_{i}").AddComponent<LineRenderer>();
                    lr.transform.SetParent(gridGO.transform, false);
                    lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    lr.material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.13f));
                    lr.widthMultiplier = 0.02f;
                    lr.positionCount = 2;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    lr.sortingOrder = 5000;
                    gridLines.Add(lr);
                }
            }
            gridGO.SetActive(true);
        }
        else
        {
            if (gridGO != null) gridGO.SetActive(false);
        }
    }

    void UpdateGrid(Vector3 snappedGhostPos, float groundY)
    {
        if (gridGO == null || !gridGO.activeSelf) return;

        int half = Mathf.RoundToInt(gridHalfCells);
        float y = groundY + gridYOffset; // <— lock to the actual ground level

        int idx = 0;
        // verticals (vary x)
        for (int x = -half; x <= half; x++)
        {
            float wx = snappedGhostPos.x + x * cellSize;
            var lr = gridLines[idx++];
            lr.SetPosition(0, new Vector3(wx, y, snappedGhostPos.z - half * cellSize));
            lr.SetPosition(1, new Vector3(wx, y, snappedGhostPos.z + half * cellSize));
        }
        // horizontals (vary z)
        for (int z = -half; z <= half; z++)
        {
            float wz = snappedGhostPos.z + z * cellSize;
            var lr = gridLines[idx++];
            lr.SetPosition(0, new Vector3(snappedGhostPos.x - half * cellSize, y, wz));
            lr.SetPosition(1, new Vector3(snappedGhostPos.x + half * cellSize, y, wz));
        }
    }

    // ===== Server spawn =====
    [Command(requiresAuthority = false)]
    void CmdPlaceBase(Vector3 pos, Quaternion rot, NetworkConnectionToClient sender = null)
    {
        var caller = sender?.identity?.GetComponent<RTSPlayer>();
        if (caller == null) { Debug.LogWarning("[BuildingPlacer] CmdPlaceBase: caller missing."); return; }

        GameObject prefab = (caller.teamId == 2) ? player2BasePrefab : player1BasePrefab;
        if (prefab == null) { Debug.LogWarning("[BuildingPlacer] CmdPlaceBase: team prefab not assigned."); return; }

        // server-side overlap check (authoritative)
        Bounds bw = GetServerPrefabBounds(prefab, pos, rot);
        Vector3 halfExtents = bw.extents * 0.98f;

        if (Physics.OverlapBox(bw.center, halfExtents, rot, buildingMask, QueryTriggerInteraction.Ignore).Length > 0)
        {
            Debug.Log("[BuildingPlacer] SERVER: placement rejected (overlap).");
            return;
        }

        var go = Instantiate(prefab, pos, rot);

        // stamp ownership
        var own = go.GetComponent<TeamOwnership>();
        if (own != null)
        {
            own.teamId = caller.teamId;
            own.ownerPlayerNetId = caller.netId;
        }

        // ensure obstacle (so units don't walk through)
        var obstacle = go.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle == null) obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.carving = true; obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;

        NetworkServer.Spawn(go);
        Debug.Log($"[BuildingPlacer] SERVER: spawned base '{prefab.name}' for team {caller.teamId} at {pos}");
    }

    Bounds GetServerPrefabBounds(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var rends = prefab.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(pos, Vector3.one * cellSize);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        // Use prefab size; center at placement pos
        return new Bounds(pos, b.size);
    }
}
