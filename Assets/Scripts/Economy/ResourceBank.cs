using Mirror;
using UnityEngine;

namespace ZeroPoint.Economy
{
    public class ResourceBank : NetworkBehaviour
    {
        [SyncVar] public int power;
        [SyncVar] public int iron;

        [Header("Generation (Power)")]
        public int powerPerSecond = 5;

        float _accum;

        void Update()
        {
            if (!isServer) return;
            _accum += Time.deltaTime;
            if (_accum >= 1f)
            {
                int ticks = Mathf.FloorToInt(_accum);
                _accum -= ticks;
                power += powerPerSecond * ticks;
            }
        }

        // SERVER: try spending resources
        [Server]
        public bool TrySpend(int powerCost, int ironCost)
        {
            if (power < powerCost || iron < ironCost) return false;
            power -= powerCost;
            iron -= ironCost;
            return true;
        }

        [Server] public void AddIron(int amount) => iron += amount;
        [Server] public void AddPower(int amount) => power += amount;
    }
}
