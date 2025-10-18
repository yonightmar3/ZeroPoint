using System.Collections.Generic;
using UnityEngine;

namespace ZeroPoint.Data
{
    public class StructureRegistry : MonoBehaviour
    {
        [Tooltip("Register all structure definitions here (drag your ScriptableObjects in.")]
        public List<StructureDefinition> definitions = new();

        private static Dictionary<string, StructureDefinition> _map;

        private void Awake()
        {
            _map = new Dictionary<string, StructureDefinition>();
            foreach (var def in definitions)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.id)) continue;

                if (_map.ContainsKey(def.id))
                {
                    Debug.LogWarning($"[StructureRegistry] Duplicate id '{def.id}' ignored.");
                    continue;
                }
                _map.Add(def.id, def);
            }
        }

        public static bool TryGet(string id, out StructureDefinition def)
        {
            def = null;
            if (_map == null) return false;
            return _map.TryGetValue(id, out def);
        }

        public static IEnumerable<StructureDefinition> All()
        {
            // Return an empty sequence if the map hasn't been initialized yet.
            if (_map == null) yield break;

            foreach (var kv in _map)
                yield return kv.Value;
        }
    }
}
