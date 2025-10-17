using UnityEngine;
using UnityEngine.InputSystem; // NEW input system
using Mirror;

public class BuildingPlacer : NetworkBehaviour
{
    public GameObject buildingPrefab;     // assign a spawnable prefab
    public LayerMask groundMask;
    public float gridSize = 1f;
    public float placementRadius = 20f;   // v0: HQ/Player radius

    Camera cam;
    GameObject ghost;
    bool placing;

    void Start()
    {
        if (!isLocalPlayer) return;
        cam = Camera.main;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null || cam == null) return; // headless safety

        // Toggle placement with B
        if (kb.bKey.wasPressedThisFrame)
        {
            placing = !placing;
            if (placing) CreateGhost();
            else DestroyGhost();
        }

        if (!placing || ghost == null) return;

        // Ray to ground
        if (Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 2000f, groundMask))
        {
            Vector3 p = hit.point;
            // snap to grid
            p.x = Mathf.Round(p.x / gridSize) * gridSize;
            p.z = Mathf.Round(p.z / gridSize) * gridSize;
            p.y = hit.point.y;

            ghost.transform.position = p;

            bool valid = Vector3.Distance(transform.position, p) <= placementRadius;
            SetGhostValid(valid);

            // Place with LMB
            if (valid && mouse.leftButton.wasPressedThisFrame)
            {
                CmdPlace(buildingPrefab != null ? buildingPrefab.name : "", p, ghost.transform.rotation);
            }
        }

        // Rotate with R
        if (kb.rKey.wasPressedThisFrame)
            ghost.transform.Rotate(0, 90, 0);
    }

    void CreateGhost()
    {
        DestroyGhost();
        if (buildingPrefab == null) return;
        ghost = Instantiate(buildingPrefab);
        foreach (var c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
        SetGhostValid(false);
    }

    void DestroyGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
    }

    void SetGhostValid(bool valid)
    {
        if (ghost == null) return;
        var rends = ghost.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            foreach (var m in r.materials)
                m.color = valid ? new Color(0, 1, 0, 0.6f) : new Color(1, 0, 0, 0.6f);
        }
    }

    [Command]
    void CmdPlace(string prefabName, Vector3 pos, Quaternion rot)
    {
        if (string.IsNullOrEmpty(prefabName)) return;
        if (!HasServerPlacementRights(pos)) return;

        GameObject prefab = FindRegisteredPrefab(prefabName);
        if (prefab == null) return; // not in NetworkManager's spawn list

        var go = Instantiate(prefab, pos, rot);
        NetworkServer.Spawn(go);
    }

    [Server]
    bool HasServerPlacementRights(Vector3 pos)
    {
        // v0 rule: within radius of player origin; later tie to HQ/Relays
        return Vector3.Distance(transform.position, pos) <= placementRadius;
    }

    // Mirror >= 80: use NetworkManager.spawnPrefabs
    GameObject FindRegisteredPrefab(string name)
    {
        var nm = NetworkManager.singleton;
        if (nm == null || nm.spawnPrefabs == null) return null;

        foreach (var p in nm.spawnPrefabs)
            if (p != null && p.name == name)
                return p;
        return null;
    }
}
