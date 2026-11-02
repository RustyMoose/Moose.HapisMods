using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Moose.Hapis.CargoPath.Behaviours;

public class CargoStopController : MonoBehaviour
{
    private static readonly Vector3 StopPosition = new(-1571.263f, 0f, 438.66f);

    private const float StopTriggerDistance = 40f;

    // CargoShip.currentThrottle / currentTurnSpeed are no longer accessible, so they are reached by reflection
    private static readonly FieldInfo ThrottleField = AccessTools.Field(typeof(CargoShip), "currentThrottle");

    private static readonly FieldInfo TurnSpeedField = AccessTools.Field(typeof(CargoShip), "currentTurnSpeed");

    private static bool _loggedMissing;

    private CargoShip? _cargoShip;

    private int _preStopTargetNodeIndex;

    private float _preStopThrottle;

    private float _preStopTurnSpeed;

    private List<Vector3> _cargoPath;

    private bool _isStopped;

    private static float GetFloat(FieldInfo field, CargoShip ship)
    {
        if (field == null)
        {
            LogMissing();
            return 0f;
        }

        return (float)field.GetValue(ship);
    }

    private static void SetFloat(FieldInfo field, CargoShip ship, float value)
    {
        if (field == null)
        {
            LogMissing();
            return;
        }

        field.SetValue(ship, value);
    }

    private static void LogMissing()
    {
        if (_loggedMissing)
        {
            return;
        }

        _loggedMissing = true;
        Debug.LogError("[Hapis.CargoPath] CargoShip.currentThrottle / currentTurnSpeed not found. Float fields: " +
            string.Join(", ", typeof(CargoShip)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(float))
                .Select(f => f.Name)));
    }

    public void SetPath(List<Vector3> path)
    {
        _cargoShip = GetComponent<CargoShip>();
        _cargoPath = path;
        if (_cargoShip == null)
        {
            Destroy(this);
            return;
        }

        Debug.Log("[Hapis.CargoPath] Controller has been attached to a new Cargo Ship.");
    }

    private void FixedUpdate()
    {
        if (_cargoShip == null || _cargoShip.IsDestroyed || _cargoPath.Count == 0)
        {
            Destroy(this);
            return;
        }

        if (!_isStopped && !_cargoShip.HasFlag(CargoShip.HasDocked) && Vector3.Distance(_cargoShip.transform.position, StopPosition) <= StopTriggerDistance)
        {
            DoCargoStop();
        }
    }

    public void DoCargoStop()
    {
        if (_cargoShip == null || _cargoShip.IsDestroyed)
        {
            Debug.LogWarning("[Hapis.CargoPath] Ship is destroyed or null. Cancelling stop.");
            Destroy(this);
            return;
        }

        Debug.Log($"[Hapis.CargoPath] Cargo Ship is entering the custom stop zone. Stopping for {CargoShip.dock_time} seconds.");

        _isStopped = true;

        _preStopTargetNodeIndex = _cargoShip.targetNodeIndex;
        _preStopThrottle = GetFloat(ThrottleField, _cargoShip);
        _preStopTurnSpeed = GetFloat(TurnSpeedField, _cargoShip);
        _cargoShip.targetNodeIndex = -1;
        SetFloat(ThrottleField, _cargoShip, 0f);
        SetFloat(TurnSpeedField, _cargoShip, 0f);

        var rigidBody = _cargoShip.GetComponent<Rigidbody>();
        if (rigidBody != null)
        {
            rigidBody.velocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
        }

        if (CargoShip.refresh_loot_on_dock)
        {
            _cargoShip?.RespawnLoot();
        }

        Invoke(nameof(PlayHorn), CargoShip.dock_time - 60f);
        Invoke(nameof(EndStop), CargoShip.dock_time);
    }

    public void PlayHorn()
    {
        _cargoShip?.PlayHorn();
    }

    public void EndStop()
    {
        if (_cargoShip == null || _cargoShip.IsDestroyed)
        {
            Debug.LogWarning("[Hapis.CargoPath] Ship is destroyed or null while trying to end stop.");
            Destroy(this);
            return;
        }

        _isStopped = false;

        _cargoShip.targetNodeIndex = _preStopTargetNodeIndex;
        SetFloat(ThrottleField, _cargoShip, _preStopThrottle);
        SetFloat(TurnSpeedField, _cargoShip, _preStopTurnSpeed);

        using (var scope = _cargoShip.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdateImmediate))
        {
            scope.Set(CargoShip.HasDocked, true);
        }
        Destroy(this);
    }
}