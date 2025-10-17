using UnityEngine;
using UnityEngine.AI;
using Mirror;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitMovement : NetworkBehaviour
{
    NavMeshAgent agent;

    void Awake() => agent = GetComponent<NavMeshAgent>();

    [Server]
    void EnsureOnNavMesh()
    {
        if (agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }

    [Server]
    public void ServerMoveTo(Vector3 destination)
    {
        EnsureOnNavMesh();
        if (!agent.isOnNavMesh) return;
        agent.SetDestination(destination);
    }

    [Server]
    public void ServerAttackMove(Vector3 destination)
    {
        ServerMoveTo(destination); // add target-scanning later
    }
}
