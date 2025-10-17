using UnityEngine;
using Mirror;

public class CustomNetworkManager : NetworkManager
{
    [Header("RTS Units per Player")]
    public GameObject player1UnitPrefab;
    public GameObject player2UnitPrefab;
    public int spawnCountPerPlayer = 6;
    public Vector3 spawnArea = new Vector3(8, 0, 8);
    public Transform player1SpawnCenter;
    public Transform player2SpawnCenter;

    public override void Awake()
    {
        base.Awake();
        // fix warning: explicitly set transport if on same object
        if (transport == null) transport = GetComponent<Transport>();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn); // spawns the RTSPlayer

        // Assign team: first connection is team 1, second is team 2
        int index = numPlayers; // after base call: 1 for first, 2 for second, etc.
        ushort team = (ushort)((index % 2 == 1) ? 1 : 2);

        var rts = conn.identity.GetComponent<RTSPlayer>();
        rts.teamId = team;

        // determine spawn info
        bool isTeam1 = team == 1;
        Transform center = isTeam1 ? player1SpawnCenter : player2SpawnCenter;
        if (center == null) center = transform;

        GameObject unitPrefab = isTeam1 ? player1UnitPrefab : player2UnitPrefab;
        if (unitPrefab == null)
        {
            Debug.LogWarning($"[{nameof(CustomNetworkManager)}] Missing unit prefab for team {team}. Using player1UnitPrefab or Player Prefab as fallback.");
            unitPrefab = player1UnitPrefab != null ? player1UnitPrefab : playerPrefab;
        }

        // spawn exactly spawnCountPerPlayer units for THIS player
        for (int i = 0; i < spawnCountPerPlayer; i++)
        {
            Vector3 pos = center.position + new Vector3(
                Random.Range(-spawnArea.x, spawnArea.x),
                0,
                Random.Range(-spawnArea.z, spawnArea.z)
            );

            var go = Instantiate(unitPrefab, pos, Quaternion.identity);

            // set ownership on server before spawn
            var own = go.GetComponent<TeamOwnership>();
            if (own != null)
            {
                own.teamId = team;
                own.ownerPlayerNetId = conn.identity.netId;
            }

            NetworkServer.Spawn(go);
        }
    }
}
