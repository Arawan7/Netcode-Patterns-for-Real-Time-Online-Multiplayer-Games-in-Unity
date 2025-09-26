using System;
using System.Collections.Generic;
using Creations;
using Creations.Types;
using Terrain;
using Unity.Netcode;
using UnityEngine;
using Utils;

public sealed class NetworkedCreationGrowingService : NetworkBehaviour
{
    public static NetworkedCreationGrowingService Singleton { get; private set; }

    #region events

    public event EventHandler<CreationSpawnedEventArgs> CreationSpawned;

    private void OnCreationSpawned(CreationSpawnedEventArgs e)
    {
        CreationSpawned?.Invoke(this, e);
    }

    public class CreationSpawnedEventArgs : EventArgs
    {
        public SpecificCreationType CreationType { get; set; }
        public byte Row { get; set; }
        public byte Column { get; set; }
        public GrowingCreationRequirements GrowingCreationRequirements { get; set; }
        public ulong ClientId { get; set; }
    }

    #endregion

    [Serializable]
    private class SpecificCreationTypeAndPrefab
    {
        public SpecificCreationType creationType;
        public GameObject creationPrefab;
    }

    [SerializeField] private List<SpecificCreationTypeAndPrefab> prefabsByCreationType;

    public void Awake()
    {
        if (Singleton != null) Destroy(this);

        Singleton = this;

        foreach (var typeAndPrefab in prefabsByCreationType)
        {
            if (typeAndPrefab.creationType == SpecificCreationType.None)
            {
                Debug.LogError($"Invalid configuration!  Type: {typeAndPrefab.creationType} is not allowed");
                Destroy(this);
            }

            if (typeAndPrefab.creationPrefab == null ||
                typeAndPrefab.creationPrefab.GetComponent<Creation>() == null
               )
            {
                Debug.LogError(
                    $"Invalid configuration! Detected type: {typeAndPrefab.creationType} with unassigned prefab or missing Creation component. Destroying myself"
                );
                Destroy(this);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        Singleton = null;
    }

    public bool TryInstantiateCreation(SpecificCreationType creationType, byte row, byte column,
        GrowingCreationRequirements growingCreationRequirements,
        ulong clientId,
        out Creation spawnedCreation
    )
    {
        spawnedCreation = null;
        if (!IsServer)
        {
            Debug.LogWarning(
                $"Cannot instantiate creation not being a server! Should instantiate creation: {creationType}, {row}, {column} for: {clientId}");
            return false;
        }

        Debug.Log($"Should spawn a: {creationType} as requested by client: {clientId} at: ({row},{column})");
        var prefab = FindPrefabForSpecificCreationType(creationType);

        var creation = prefab.GetComponent<Creation>();
        if (CreationGrowingValidator.IsGrowingValid(row, column, creation, growingCreationRequirements, clientId))
        {
            spawnedCreation = SpawnPrefabWithOwnership(clientId, row, column, prefab,
                growingCreationRequirements is { BindCreations: true } ||
                creationType.Equals(SpecificCreationType.Hunter)
            );

            OnCreationSpawned(new CreationSpawnedEventArgs
            {
                CreationType = creationType,
                Row = row,
                Column = column,
                GrowingCreationRequirements = growingCreationRequirements,
                ClientId = clientId
            });
            Debug.Log($"Spawned a: {creationType} owned by client: {clientId}");
            return true;
        }

        return false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void TryInstantiateCreationServerRpc(SpecificCreationType creationType, byte row, byte column,
        GrowingCreationRequirements growingCreationRequirements,
        ServerRpcParams serverRpcParams = default
    )
    {
        var clientId = serverRpcParams.Receive.SenderClientId;
        TryInstantiateCreation(
            creationType, row, column, growingCreationRequirements, clientId, out var spawnedCreation
        );
    }

    [ServerRpc(RequireOwnership = false)]
    public void TryDestroyCreationServerRpc(byte row, byte column, DeathReason deathReason,
        ServerRpcParams serverRpcParams = default)
    {
        var clientId = serverRpcParams.Receive.SenderClientId;
        Debug.Log($"Should destroy a creation as requested by client: {clientId} at: ({row},{column})");
        if (Terrain.Terrain.Singleton.TryGetCreation(row, column, out var creation))
        {
            if (creation.FactionId == clientId &&
                creation.SpecificCreationType != SpecificCreationType.MotherShroomlingTier1 &&
                creation.SpecificCreationType != SpecificCreationType.MotherShroomlingTier2 &&
                creation.SpecificCreationType != SpecificCreationType.MotherShroomlingTier3
               )
                DestroyNetworkedCreation(creation, deathReason);
        }
    }

    private static void DestroyNetworkedCreation(Creation creation, DeathReason deathReason)
    {
        creation.DeathReason = deathReason;
        var creationGameObject = creation.gameObject;
        Debug.Log($"Destroying: {creationGameObject.name}");
        // despawn to avoid overwriting creation at terrain tile (Destroy will lead to this in the next frame) 
        creationGameObject.GetComponent<NetworkObject>().Despawn();
        // TODO: use object pooling
        Destroy(creationGameObject);
    }

    public GameObject FindPrefabForSpecificCreationType(SpecificCreationType creationType)
    {
        var typeAndPrefab = prefabsByCreationType.Find(typeAndPrefab => typeAndPrefab.creationType == creationType);
        if (typeAndPrefab == null)
        {
            throw new Exception(
                $"Could not find prefab for creationType: {creationType}. Is it assigned in the inspector?"
            );
        }

        return typeAndPrefab.creationPrefab;
    }
}