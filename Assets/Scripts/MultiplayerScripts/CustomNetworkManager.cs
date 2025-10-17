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

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn); // spawns RTSPlayer

        int index = numPlayers; // 1-based after base call
        bool isFirst = (index == 1);

        Transform center = isFirst ? player1SpawnCenter : player2SpawnCenter;
        if (center == null) center = transform;

        GameObject unitPrefab = isFirst ? player1UnitPrefab : player2UnitPrefab;
        if (unitPrefab == null)
        {
            Debug.LogWarning($"[{nameof(CustomNetworkManager)}] Unit prefab for player {(isFirst ? 1 : 2)} not assigned. Falling back to player1UnitPrefab or Player Prefab.");
            unitPrefab = player1UnitPrefab != null ? player1UnitPrefab : playerPrefab;
        }

        for (int i = 0; i < spawnCountPerPlayer; i++)
        {
            Vector3 pos = center.position + new Vector3(
                Random.Range(-spawnArea.x, spawnArea.x), 0, Random.Range(-spawnArea.z, spawnArea.z)
            );
            var go = Instantiate(unitPrefab, pos, Quaternion.identity);
            NetworkServer.Spawn(go);
        }
    }
}
