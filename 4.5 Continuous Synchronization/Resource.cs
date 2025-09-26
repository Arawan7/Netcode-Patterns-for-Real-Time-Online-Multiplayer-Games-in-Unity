using System;
using System.Collections.Generic;
using System.Linq;
using Balancing;
using CustomAttributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public class Resource : NetworkBehaviourWithBalancing
{
    [SerializeField, ApplyBalancing] protected float initialProductivityPerMinute;
    [SerializeField, ApplyBalancing] protected float thresholdProductivityPerMinute;

    [FormerlySerializedAs("minutesToReachThreshold")] [SerializeField, ApplyBalancing]
    private float secondsToReachThreshold;

    private bool _decrease;

    private readonly NetworkVariable<float> _currentProductivityPerMinute = new();
    private readonly NetworkVariable<float> _productivityChangePerMinute = new();
    private readonly List<SporeIncomeGeneratorServer> _sporeIncomeGenerators = new();

    private Transform[] _ecoScaling;

    private new void Awake()
    {
        base.Awake();
        enabled = false;

        _ecoScaling = transform.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("EcoScaling"))
            .ToArray();

        foreach (var t in _ecoScaling)
        {
            foreach (Transform percentage in t)
            {
                percentage.gameObject.SetActive(false);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            enabled = true;
            _currentProductivityPerMinute.Value = initialProductivityPerMinute;
            // avoid division by zero later on, i.e., take a minute if it should instantly be reached
            secondsToReachThreshold = Math.Max(secondsToReachThreshold, 1);

            // _productivityChangePerMinute is (how much needed productivity change) / (secondsToReachThreshold / 60)
            _productivityChangePerMinute.Value =
                Math.Abs(initialProductivityPerMinute - thresholdProductivityPerMinute) /
                (secondsToReachThreshold / 60);

            _decrease = initialProductivityPerMinute > thresholdProductivityPerMinute;
            if (_decrease)
                _productivityChangePerMinute.Value *= -1;
        }

        if (IsClient)
        {
            _currentProductivityPerMinute.OnValueChanged += HandleEcoScaling;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer)
        {
            // unlink resource
            foreach (var sporeIncomeGeneratorServer in _sporeIncomeGenerators)
            {
                sporeIncomeGeneratorServer.UnlinkResource(this);
            }
        }

        if (IsClient)
        {
            _currentProductivityPerMinute.OnValueChanged -= HandleEcoScaling;
        }
    }

    protected void Update()
    {
        var newProductivityPerMinute = _currentProductivityPerMinute.Value +
                                       _productivityChangePerMinute.Value * _sporeIncomeGenerators.Count *
                                       Time.fixedDeltaTime / 60;

        _currentProductivityPerMinute.Value = _decrease
            ? Math.Max(newProductivityPerMinute, thresholdProductivityPerMinute)
            : Math.Min(newProductivityPerMinute, thresholdProductivityPerMinute);

        // apply _currentProductivityPerMinute to all subscribed sporeIncomeGenerators
        foreach (var sporeIncomeGeneratorServer in _sporeIncomeGenerators)
        {
            sporeIncomeGeneratorServer.SetSporeIncomePerMinute(_currentProductivityPerMinute.Value);
        }
    }

    public void Subscribe(SporeIncomeGeneratorServer sporeIncomeGeneratorServer)
    {
        _sporeIncomeGenerators.Add(sporeIncomeGeneratorServer);
    }

    public void Unsubscribe(SporeIncomeGeneratorServer sporeIncomeGeneratorServer)
    {
        _sporeIncomeGenerators.Remove(sporeIncomeGeneratorServer);
    }

    private void HandleEcoScaling(float oldProductivity, float newProductivity)
    {
        var toReach = Math.Abs(initialProductivityPerMinute - thresholdProductivityPerMinute);
        var reached = Math.Abs(initialProductivityPerMinute - newProductivity);
        var reachedEcoScalingPercentage = (int)(100 / toReach * reached);

        foreach (var t in _ecoScaling)
        {
            foreach (Transform percentage in t)
            {
                if (int.Parse(percentage.name) <= reachedEcoScalingPercentage)
                {
                    percentage.gameObject.SetActive(true);
                }
            }
        }
    }
}