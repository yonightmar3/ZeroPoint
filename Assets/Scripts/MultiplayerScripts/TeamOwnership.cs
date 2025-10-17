using Mirror;
using UnityEngine;

public class TeamOwnership : NetworkBehaviour
{
    [SyncVar] public ushort teamId;          // 1 or 2
    [SyncVar] public uint ownerPlayerNetId;  // the RTSPlayer netId that owns this unit

    public bool IsOwnedBy(NetworkConnectionToClient conn)
    {
        if (conn == null || conn.identity == null) return false;
        return conn.identity.netId == ownerPlayerNetId;
    }
}
