using UnityEngine;
using Mirror;

public class UnitSpawner : NetworkBehaviour
{
    public GameObject unitPrefab;
    public int count = 5;
    public Vector3 area = new Vector3(10, 0, 10);

    public override void OnStartServer()
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = transform.position + new Vector3(
                Random.Range(-area.x, area.x),
                0,
                Random.Range(-area.z, area.z)
            );
            var go = Instantiate(unitPrefab, pos, Quaternion.identity);
            NetworkServer.Spawn(go); // replicate to all clients
        }
    }
}
