using System.Collections.Generic;
using UnityEngine;

namespace Moose.Hapis.CargoPath.Behaviours;

public class CargoStopController : MonoBehaviour
{
    private static readonly Vector3 StopPosition = new(-1571.263f, 0f, 438.66f);

	private const float StopTriggerDistance = 40f;

	private CargoShip? _cargoShip;

	private int _preStopTargetNodeIndex;
	
	private float _preStopThrottle;
	
	private float _preStopTurnSpeed;

	private List<Vector3> _cargoPath;

	private bool _isStopped;

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
		_preStopThrottle = _cargoShip.currentThrottle;
		_preStopTurnSpeed = _cargoShip.currentTurnSpeed;
		_cargoShip.targetNodeIndex = -1;
		_cargoShip.currentThrottle = 0f;
		_cargoShip.currentTurnSpeed = 0f;
			
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
		_cargoShip.currentThrottle = _preStopThrottle;
		_cargoShip.currentTurnSpeed = _preStopTurnSpeed;
		
		using (var scope = _cargoShip.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdateImmediate))
		{
			scope.Set(CargoShip.HasDocked, true);
		}
		Destroy(this);
	}
}