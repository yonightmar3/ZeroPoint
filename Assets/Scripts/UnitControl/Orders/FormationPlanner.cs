using System.Collections.Generic;
using UnityEngine;

namespace ZeroPoint.Orders
{
    public static class FormationPlanner
    {
        // Simple square grid around center, spacing = radius * factor
        public static List<Vector3> GridSlots(Vector3 center, int count, float spacing)
        {
            var result = new List<Vector3>(count);
            if (count <= 0) return result;

            int side = Mathf.CeilToInt(Mathf.Sqrt(count));
            int placed = 0;

            float half = (side - 1) * 0.5f;
            for (int r = 0; r < side && placed < count; r++)
                for (int c = 0; c < side && placed < count; c++)
                {
                    float x = (c - half) * spacing;
                    float z = (r - half) * spacing;
                    result.Add(center + new Vector3(x, 0, z));
                    placed++;
                }
            return result;
        }
    }
}
