using UnityEngine;

namespace ZeroPoint.Data
{
    public enum StructureCategory { HQ, Relay, Mine, Conveyor, Turret, Shield, Factory, Other }

    [CreateAssetMenu(fileName = "StructureDefinition", menuName = "ZeroPoint/Structure Definition")]
    public class StructureDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique short id used over the network (no spaces).")]
        public string id = "Base";

        [Header("Prefab References")]
        [Tooltip("Team 1 prefab (spawnable, has NetworkIdentity).")]
        public GameObject team1Prefab;
        [Tooltip("Team 2 prefab (spawnable, has NetworkIdentity).")]
        public GameObject team2Prefab;

        [Header("Rules & Costs")]
        public StructureCategory category = StructureCategory.Other;
        [Tooltip("Power cost to place")] public int powerCost = 0;
        [Tooltip("Iron cost to place")] public int ironCost = 0;

        [Header("Placement")]
        [Tooltip("Override grid cell size; 0 = use global.")]
        public float overrideGridCell = 0f;
        [Tooltip("Required radius from HQ; 0 = no limit (we’ll add relays later).")]
        public float requiredRadiusFromHQ = 0f;
    }
}
