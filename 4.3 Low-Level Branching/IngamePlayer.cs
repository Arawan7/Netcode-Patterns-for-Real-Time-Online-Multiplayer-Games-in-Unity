using System.Collections.Generic;
using CreationGrowing;
using Unity.Netcode;
using UnityEngine;

public class IngamePlayer : NetworkBehaviour
{
    private readonly NetworkVariable<byte> _unlockedTier = new();
    public byte UnlockedTier => _unlockedTier.Value;

    private static readonly List<CreationGrowingUIHandler> UIHandlers = new();

    public static void RegisterCreationGrowingUIHandler(CreationGrowingUIHandler handler)
    {
        UIHandlers.Add(handler);
    }

    public static void DeregisterCreationGrowingUIHandler(CreationGrowingUIHandler handler)
    {
        UIHandlers.Remove(handler);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        FaceMainCameraToMotherShroomling();
        InGameStateManager.Singleton.OnMatchEndSequenceFinished += FaceMainCameraToMotherShroomling;

        if (IsClient)
        {
            _unlockedTier.OnValueChanged += (tier, newTier) =>
            {
                if (NetworkManager.Singleton.LocalClientId == OwnerClientId)
                {
                    foreach (var handler in UIHandlers)
                    {
                        handler.HandleLockedIconForNewTier(newTier);
                    }
                }
            };
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        InGameStateManager.Singleton.OnMatchEndSequenceFinished -= FaceMainCameraToMotherShroomling;
    }

    private void FaceMainCameraToMotherShroomling()
    {
        if (NetworkManager.LocalClientId == OwnerClientId)
        {
            Debug.Log($"Face main camera to mother shroomling. I am client: {NetworkManager.LocalClientId}");
            if (Camera.main != null)
            {
                var motherShroomlingTransform = transform;
                Transform cameraTransform;
                // rotate the root object to keep movement direction logic in CameraController working
                (cameraTransform = Camera.main.transform.parent.parent).eulerAngles =
                    motherShroomlingTransform.rotation.eulerAngles;
                cameraTransform.position =
                    motherShroomlingTransform.position - 14 * motherShroomlingTransform.forward
                    + new Vector3(0, 20, 0);
                // set the camera rotation to 50 degrees in the x axis
                cameraTransform.Find("CameraRotation").localEulerAngles = new Vector3(50, 0, 0);
            }
        }
    }

    public void UnlockNextTier()
    {
        if (IsServer)
        {
            _unlockedTier.Value++;
        }
    }
}