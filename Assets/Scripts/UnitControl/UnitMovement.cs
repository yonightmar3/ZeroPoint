using UnityEngine;
using UnityEngine.AI;
using Mirror;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitMovement : NetworkBehaviour
{
    NavMeshAgent agent;

    void Awake() => agent = GetComponent<NavMeshAgent>();

    [Server]
    public void ServerMoveTo(Vector3 destination)
    {
        if (!agent.isOnNavMesh) return;
        agent.SetDestination(destination);
    }

    [Server]
    public void ServerAttackMove(Vector3 destination)
    {
        // v0: identical to move; later, add target-scanning while moving.
        ServerMoveTo(destination);
    }
}
