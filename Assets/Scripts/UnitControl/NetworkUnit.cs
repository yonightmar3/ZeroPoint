using UnityEngine;
using Mirror;

[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(Selectable))]
public class NetworkUnit : NetworkBehaviour
{
    UnitMovement movement;
    Selectable selectable;

    void Awake()
    {
        movement = GetComponent<UnitMovement>();
        selectable = GetComponent<Selectable>();
    }

    // client-side selection feedback
    [ClientCallback] void OnEnable() => selectable.SetSelected(false);

    // === client → server orders ===
    [Command] // called by the **local** client that owns the unit OR by a player commander (see below)
    public void CmdMove(Vector3 dest) => movement.ServerMoveTo(dest);

    [Command]
    public void CmdAttackMove(Vector3 dest) => movement.ServerAttackMove(dest);
}
