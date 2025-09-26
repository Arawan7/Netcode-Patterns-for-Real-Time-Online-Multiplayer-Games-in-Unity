using Balancing;
using Creations;
using Terrain;
using UnityEngine;
using Utils;

[RequireComponent(typeof(Creation))]
public class SporeIncomeGeneratorServer : NetworkBehaviourWithBalancing
{
    private float _sporeIncomePerMinute;
    private ResourceManager _factionResourceManager;
    private Resource _resourceToHarvestFrom;
    private Creation _creation;

    protected new void Awake()
    {
        base.Awake();

        _creation = GetComponent<Creation>();

        enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        enabled = true;

        if (FindAndAssignResourceToHarvestFrom())
        {
            _resourceToHarvestFrom.Subscribe(this);
            Debug.Log($"Getting ResourceManager for factionId: {OwnerClientId}");
            if (!InGamePlayerUtil.GetResourceManager(OwnerClientId, out _factionResourceManager))
            {
                Debug.LogError(
                    $"Could not get ResourceManager and therefore, cannot increase spore income for: {OwnerClientId}");
            }
        }
        else
        {
            Debug.LogWarning(
                $"Could not find a Resource to harvest from and therefore, cannot generate spore income for: {OwnerClientId}");
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (!IsServer)
            return;

        if (_resourceToHarvestFrom != null)
            _resourceToHarvestFrom.Unsubscribe(this);

        if (_factionResourceManager != null)
            _factionResourceManager.IncreaseSporeIncomePerMinute(-1 * _sporeIncomePerMinute);
        _factionResourceManager = null;
    }

    public void SetSporeIncomePerMinute(float productivityPerMinute)
    {
        var previousSporeIncomePerMinute = _sporeIncomePerMinute;
        // determine the new _sporeIncomePerMinute
        // here we might also apply productivity factor to achieve different productivity dependent on SporeIncomeGenerator instead of fully depending on the resource only
        // for now, we just apply the productivityPerMinute
        _sporeIncomePerMinute = productivityPerMinute;
        // apply the change
        _factionResourceManager.IncreaseSporeIncomePerMinute(_sporeIncomePerMinute - previousSporeIncomePerMinute);
    }

    private bool FindAndAssignResourceToHarvestFrom()
    {
        if (Terrain.Terrain.Singleton.TryGetCreation(
                _creation.SpawnCoord.x, _creation.SpawnCoord.y, out var creation
            ))
        {
            var resource = creation.gameObject.GetComponent<Resource>();
            if (resource != null)
            {
                _resourceToHarvestFrom = resource;
                return true;
            }
        }

        foreach (var coord in TerrainGridMath.GetSmallRingCoords(_creation.SpawnCoord.x, _creation.SpawnCoord.y))
        {
            if (Terrain.Terrain.Singleton.TryGetTerrainTile(coord, out var terrainTile))
            {
                var resource = terrainTile.gameObject.GetComponent<Resource>();
                if (resource != null)
                {
                    _resourceToHarvestFrom = resource;
                    return true;
                }
            }
        }

        _resourceToHarvestFrom = null;
        return false;
    }

    public void UnlinkResource(Resource resource)
    {
        _resourceToHarvestFrom = null;
    }
}