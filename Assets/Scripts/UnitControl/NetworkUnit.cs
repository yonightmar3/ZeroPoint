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

    [ClientCallback] void OnEnable() => selectable.SetSelected(false);

    // IMPORTANT: allow any client to request orders (server validates rules)
    [Command(requiresAuthority = false)]
    public void CmdMove(Vector3 dest) => movement.ServerMoveTo(dest);

    [Command(requiresAuthority = false)]
    public void CmdAttackMove(Vector3 dest) => movement.ServerAttackMove(dest);
}
