using Mirror;
using UnityEngine;
using ZeroPoint.Data;
using ZeroPoint.Gameplay;
using ZeroPoint.Economy;

namespace ZeroPoint.Orders
{
    [RequireComponent(typeof(ResourceBank))]
    public class OrdersBuild : NetworkBehaviour
    {
        [Header("Masks & Grid")]
        public LayerMask groundMask;
        public LayerMask buildingMask;
        public float gridCellSize = 2f;

        ResourceBank bank;
        RTSPlayer rtsPlayer; // to get teamId & HQ later

        void Awake()
        {
            bank = GetComponent<ResourceBank>();
            rtsPlayer = GetComponent<RTSPlayer>();
        }

        public override void OnStartClient()
        {
            // keep validator settings in sync on both ends (client uses it for ghost snap)
            PlacementValidator.GlobalCellSize = gridCellSize;
            PlacementValidator.BuildingMask = buildingMask;
        }

        // ====== CLIENT-SIDE ENTRY ======
        // Call this from UI (cards) or hotkeys with the defId and pose.
        [Client]
        public void RequestBuild(string defId, Vector3 pos, Quaternion rot)
        {
            // local snap for consistency
            if (!StructureRegistry.TryGet(defId, out var def)) { Debug.LogWarning($"[OrdersBuild] No def '{defId}'"); return; }

            var snapped = PlacementValidator.SnapToGrid(pos, def.overrideGridCell);
            CmdOrderBuild(def.id, snapped, rot);
        }

        // ====== SERVER ======
        [Command(requiresAuthority = false)]
        void CmdOrderBuild(string defId, Vector3 snappedPos, Quaternion rot, NetworkConnectionToClient sender = null)
        {
            var caller = sender?.identity?.GetComponent<RTSPlayer>();
            if (caller == null) return;

            if (!StructureRegistry.TryGet(defId, out var def)) return;

            // choose team prefab
            var prefab = caller.teamId == 2 ? def.team2Prefab : def.team1Prefab;
            if (prefab == null) { Debug.LogWarning($"[OrdersBuild] Missing prefab for team in def '{defId}'"); return; }

            // validate overlap
            PlacementValidator.BuildingMask = buildingMask;
            PlacementValidator.GlobalCellSize = gridCellSize;

            if (PlacementValidator.OverlapsBuilding(prefab, snappedPos, rot))
            {
                Debug.Log("[OrdersBuild] Reject: overlap.");
                return;
            }

            // (Optional) HQ radius — disabled by default unless def.requiredRadiusFromHQ > 0.
            if (def.requiredRadiusFromHQ > 0f)
            {
                Vector3 hqPos = caller.GetHQPosition(); // extension on RTSPlayer below
                if (!PlacementValidator.WithinHQRadius(snappedPos, hqPos, def.requiredRadiusFromHQ))
                {
                    Debug.Log("[OrdersBuild] Reject: out of HQ radius.");
                    return;
                }
            }

            // cost check (team bank lives on the caller for now)
            var bank = caller.GetComponent<ResourceBank>();
            if (bank == null || !bank.TrySpend(def.powerCost, def.ironCost))
            {
                Debug.Log("[OrdersBuild] Reject: insufficient resources.");
                return;
            }

            // spawn
            var go = Instantiate(prefab, snappedPos, rot);

            // ensure team is stamped
            var own = go.GetComponent<TeamOwnership>();
            if (own != null) { own.teamId = caller.teamId; own.ownerPlayerNetId = caller.netId; }

            // ensure obstacle for pathing
            var obst = go.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obst == null) obst = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obst.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obst.carving = true;

            NetworkServer.Spawn(go);
        }
    }

    // RTSPlayer helper so OrdersBuild can find HQ (simple stub for now)
    public static class RTSPlayerExtensions
    {
        public static Vector3 GetHQPosition(this RTSPlayer p)
        {
            // If you tag your HQ prefab "HQ", you can find your own HQ by team.
            // For Phase 1 we just return player's first spawned base if you store it later.
            // Temporary: origin (disabled by radius=0 for now).
            return Vector3.zero;
        }
    }
}
