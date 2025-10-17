using UnityEngine;
using Mirror;

[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(Selectable))]
[RequireComponent(typeof(TeamOwnership))]
public class NetworkUnit : NetworkBehaviour
{
    UnitMovement movement;
    Selectable selectable;
    TeamOwnership ownership;

    void Awake()
    {
        movement = GetComponent<UnitMovement>();
        selectable = GetComponent<Selectable>();
        ownership = GetComponent<TeamOwnership>();
    }

    [ClientCallback] void OnEnable() => selectable.SetSelected(false);

    // Allow any client to request; server validates using the *sender* param.
    [Command(requiresAuthority = false)]
    public void CmdMove(Vector3 dest, NetworkConnectionToClient sender = null)
    {
        if (!IsCallerMyOwnerTeam(sender)) return;
        movement.ServerMoveTo(dest);
    }

    [Command(requiresAuthority = false)]
    public void CmdAttackMove(Vector3 dest, NetworkConnectionToClient sender = null)
    {
        if (!IsCallerMyOwnerTeam(sender)) return;
        movement.ServerAttackMove(dest);
    }

    bool IsCallerMyOwnerTeam(NetworkConnectionToClient sender)
    {
        var caller = sender?.identity?.GetComponent<RTSPlayer>();
        if (caller == null) return false;
        return caller.teamId == ownership.teamId;
    }
}
