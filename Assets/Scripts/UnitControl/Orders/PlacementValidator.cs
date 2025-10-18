using UnityEngine;
using ZeroPoint.Data;

namespace ZeroPoint.Gameplay
{
    public static class PlacementValidator
    {
        // Configure these from your existing systems
        public static float GlobalCellSize = 2f;
        public static LayerMask BuildingMask;
        public static float GridYOffset = 0.01f;

        // GRID SNAP
        public static Vector3 SnapToGrid(Vector3 world, float? overrideCell = null)
        {
            float cell = overrideCell.HasValue && overrideCell.Value > 0f ? overrideCell.Value : GlobalCellSize;
            float gx = Mathf.Round(world.x / cell) * cell;
            float gz = Mathf.Round(world.z / cell) * cell;
            return new Vector3(gx, world.y, gz);
        }

        // Overlap check using bounds of prefab at desired pose
        public static bool OverlapsBuilding(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            // Approximate from renderers on prefab
            var rends = prefab.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return false;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            var halfExtents = b.extents * 0.98f;

            var hits = Physics.OverlapBox(pos, halfExtents, rot, BuildingMask, QueryTriggerInteraction.Ignore);
            return hits.Length > 0;
        }

        // Optional HQ radius check—pass 0 to disable for now
        public static bool WithinHQRadius(Vector3 pos, Vector3 hqPos, float requiredRadius)
        {
            if (requiredRadius <= 0f) return true;
            return Vector3.Distance(pos, hqPos) <= requiredRadius;
        }
    }
}
