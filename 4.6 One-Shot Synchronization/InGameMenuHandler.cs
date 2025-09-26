using System;

public class InGameMenuHandler : NetworkBehaviour
{
    // [...]

    public void Resign()
    {
        Debug.Log("Telling server I want to resign");
        ResignServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResignServerRpc(ServerRpcParams serverRpcParams = default)
    {
        InGameStateManager.Singleton.HandleMainCreationDying(serverRpcParams.Receive.SenderClientId);
    }
}