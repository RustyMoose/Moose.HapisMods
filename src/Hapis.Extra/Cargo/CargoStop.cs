using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.Extra;

internal class CargoStop : MonoBehaviour
{
	private const float TakeOver = 180f;
	private const float LookAhead = 25f;
	private const float LineOvershoot = 20f;
	private const float FinalRun = 90f;
	private const float TurnRate = 4f;
	private const float DockSpeed = 4f;
	private const float CreepSpeed = 0.6f;
	private const float SlowAngle = 25f;
	private const float Accel = 0.4f;
	private const float SlideSpeed = 0.4f;
	private const float SettleDistance = 0.4f;
	private const float SettleAngle = 0.3f;
	private const float LeaveSpeed = 6f;
	private const float LeaveDistance = 250f;
	private const float NodeReach = 30f;
	private const float DockOffset = 15f;
	private const float EaseSpeed = 1.5f;
	private const float EaseOutSpeed = 2f;
	private const float EaseOut = 25f;
	private const float EaseForward = 1f;
	private const float EaseAngle = 45f;
	private const float HalfLength = 65f;
	private const float HalfBeam = 12f;
	private const float StraightAngle = 10f;
	private const float PauseTime = 1.5f;
	private const float GuardAhead = 10f;
	private const int GuardTurns = 4;
	private const int GuardPoints = 13;
	private const float BargeClear = 2f;
	private const float BargeNear = 15f;
	private const float BargeFar = 35f;
	private const float BargeFrom = -1.5f;
	private const float BargeTo = 48.5f;
	private const float ShoreDepth = 0.5f;

	private static readonly FieldInfo? Throttle = AccessTools.Field(typeof(CargoShip), "currentThrottle");
	private static readonly FieldInfo? Turning = AccessTools.Field(typeof(CargoShip), "currentTurnSpeed");
	private static readonly FieldInfo? Velocity = AccessTools.Field(typeof(CargoShip), "currentVelocity");
	private static readonly FieldInfo? Lifetime = AccessTools.Field(typeof(CargoShip), "lifetime");
	private static readonly FieldInfo? Egressing = AccessTools.Field(typeof(CargoShip), "egressing");
	private static readonly FieldInfo? PatrolPath = AccessTools.Field(typeof(TerrainPath), "OceanPatrolFar");
	private static readonly Dictionary<CargoShip, CargoStop> Driving = new();

	private enum Phase
	{
		Watching,
		Approach,
		Settle,
		Hold,
		EaseOut,
		Leave
	}

	private CargoShip? _ship;
	private List<Vector3> _stops = new();
	private List<Vector3> _path = new();
	private Phase _phase;
	private Vector3 _spot;
	private Vector3 _dock;
	private Vector3 _away;
	private Vector3 _heading;
	private float _speed;
	private int _node;
	private float _left;
	private float _paused;
	private float _slid;
	private bool _turning;
	private bool _easing;
	private bool _timerPaused;
	private bool _egressDue;
	private float _frozenLifetime;

	public void Begin(CargoShip ship, List<Vector3> stops)
	{
		_ship = ship;
		_stops = stops;
		InvokeRepeating(nameof(Watch), 1f, 1f);
	}

	private static List<Vector3> ShipPath()
	{
		if (TerrainMeta.Path != null && PatrolPath?.GetValue(TerrainMeta.Path) is List<Vector3> path)
		{
			return path;
		}

		return new List<Vector3>();
	}

	private static bool IsEgressing(CargoShip ship)
	{
		return ship.HasFlag(CargoShip.Egressing) || Egressing?.GetValue(ship) is true;
	}

	public static bool Steer(CargoShip ship)
	{
		if (!Driving.TryGetValue(ship, out var stop) || stop == null)
		{
			return false;
		}

		stop.Step(Time.fixedDeltaTime);
		return true;
	}

	private void OnDestroy()
	{
		if (_ship != null)
		{
			ResumeTimer();
			Driving.Remove(_ship);
		}
	}

	private void Watch()
	{
		if (_ship == null || _ship.IsDestroyed)
		{
			Destroy(this);
			return;
		}

		if (_phase != Phase.Watching || _ship.HasFlag(CargoShip.HasDocked) || IsEgressing(_ship))
		{
			return;
		}

		var here = _ship.transform.position;
		foreach (var stop in _stops)
		{
			if (Flat(here - stop).magnitude <= TakeOver)
			{
				Take(stop);
				return;
			}
		}
	}

	private void Take(Vector3 stop)
	{
		var ship = _ship!;
		CancelInvoke(nameof(Watch));

		_path = ShipPath();
		_dock = new Vector3(stop.x, ship.transform.position.y, stop.z);
		_heading = Heading(stop, ship.transform.forward);
		_away = Vector3.Cross(Vector3.up, _heading).normalized;
		_spot = _dock + _away * DockOffset;
		_speed = Read(Throttle, ship) * 8f;
		_node = _path.Count > 0 ? ship.targetNodeIndex : -1;
		_slid = 0f;
		_phase = Phase.Approach;
		Driving[ship] = this;
	}

	private Vector3 Heading(Vector3 stop, Vector3 fallback)
	{
		if (_path.Count < 3)
		{
			return Flat(fallback).normalized;
		}

		var nearest = 0;
		var best = float.MaxValue;
		for (var i = 0; i < _path.Count; i++)
		{
			var gap = Flat(_path[i] - stop).sqrMagnitude;
			if (gap < best)
			{
				best = gap;
				nearest = i;
			}
		}

		var before = _path[(nearest + 1) % _path.Count];
		var after = _path[(nearest - 1 + _path.Count) % _path.Count];
		var way = Flat(after - before);
		return way.sqrMagnitude > 0.01f ? way.normalized : Flat(fallback).normalized;
	}

	private void Step(float dt)
	{
		if (_ship == null || _ship.IsDestroyed)
		{
			Destroy(this);
			return;
		}

		if ((_phase == Phase.Approach || _phase == Phase.Settle) && IsEgressing(_ship))
		{
			Log.Info("Cargo: ship started leaving the map before it docked, docking cancelled");
			Release(_ship.targetNodeIndex);
			return;
		}

		switch (_phase)
		{
			case Phase.Approach:
				Approach(dt);
				break;
			case Phase.Settle:
				Settle(dt);
				break;
			case Phase.Hold:
				Write(Velocity, _ship, Vector3.zero);
				if (_timerPaused)
				{
					Write(Lifetime, _ship, _frozenLifetime);
				}

				break;
			case Phase.EaseOut:
				EaseAway(dt);
				break;
			case Phase.Leave:
				Leave(dt);
				break;
		}
	}

	private void Approach(float dt)
	{
		var ship = _ship!;
		var here = ship.transform.position;
		var along = Vector3.Dot(Flat(here - _dock), _heading);
		if (along >= -1f)
		{
			_phase = Phase.Settle;
			return;
		}

		var straightness = Mathf.Abs(Vector3.SignedAngle(Flat(ship.transform.forward), _heading, Vector3.up));
		if (along >= -FinalRun && straightness < StraightAngle)
		{
			_easing = true;
		}

		if (_easing)
		{
			Turn(_heading, dt);
			Slide(ship, dt);
			Move(Mathf.Clamp(-along * 0.08f, CreepSpeed, DockSpeed), dt);
			return;
		}

		if (along >= -FinalRun && straightness < EaseAngle)
		{
			_slid -= Slide(ship, dt);
		}

		var aim = _spot - _away * _slid + _heading * Mathf.Min(along + LookAhead, LineOvershoot);
		if (along < -FinalRun && _node >= 0 && _node < _path.Count)
		{
			while (Flat(_path[_node] - here).magnitude < NodeReach)
			{
				_node = (_node - 1 + _path.Count) % _path.Count;
			}

			aim = _path[_node];
		}

		var toward = Flat(aim - ship.transform.position);
		var off = Mathf.Abs(Vector3.SignedAngle(Flat(ship.transform.forward), toward, Vector3.up));
		if (off > SlowAngle && !_turning)
		{
			Move(0f, dt);
			if (_speed <= 0.05f)
			{
				_paused += dt;
			}

			_turning = _paused >= PauseTime;
			return;
		}

		if (off < StraightAngle)
		{
			_turning = false;
			_paused = 0f;
		}

		Turn(toward, dt);
		Move(off > SlowAngle ? CreepSpeed : Mathf.Clamp(-along * 0.08f, CreepSpeed, DockSpeed), dt);
	}

	private float Slide(CargoShip ship, float dt)
	{
		var here = ship.transform.position;
		var lateral = Vector3.Dot(Flat(_dock - here), _away);
		var step = Mathf.Clamp(lateral, -EaseSpeed * dt, EaseSpeed * dt);
		if (step < 0f && Blocked(ship, here + _away * step))
		{
			return 0f;
		}

		ship.transform.position = here + _away * step;
		return step;
	}

	private bool Blocked(CargoShip ship, Vector3 centre)
	{
		var forward = Flat(ship.transform.forward).normalized;
		var left = Vector3.SignedAngle(forward, _heading, Vector3.up);
		for (var k = 0; k <= GuardTurns; k++)
		{
			var facing = Quaternion.AngleAxis(left * k / GuardTurns, Vector3.up) * forward;
			var side = Vector3.Cross(Vector3.up, facing);
			for (var ahead = 0f; ahead <= GuardAhead; ahead += GuardAhead / 2f)
			{
				for (var i = 0; i <= GuardPoints; i++)
				{
					var length = Mathf.Lerp(-HalfLength, HalfLength, (float)i / GuardPoints);
					var point = centre + facing * (ahead + length);
					if (Hits(point + side * HalfBeam) || Hits(point - side * HalfBeam))
					{
						return true;
					}
				}
			}
		}

		return false;
	}

	private bool Hits(Vector3 point)
	{
		var offset = Flat(point - _dock);
		var across = -Vector3.Dot(offset, _away);
		var along = Vector3.Dot(offset, _heading);
		var x = Mathf.Max(BargeNear - across, 0f, across - BargeFar);
		var z = Mathf.Max(BargeFrom - along, 0f, along - BargeTo);
		if (x * x + z * z < BargeClear * BargeClear)
		{
			return true;
		}

		return TerrainMeta.HeightMap != null && TerrainMeta.HeightMap.GetHeight(point) > -ShoreDepth;
	}

	private void Settle(float dt)
	{
		var ship = _ship!;
		var off = Turn(_heading, dt);
		_speed = Mathf.MoveTowards(_speed, 0f, Accel * 2f * dt);

		var here = ship.transform.position;
		var gap = Flat(_dock - here);
		var slide = Vector3.ClampMagnitude(gap, SlideSpeed * dt);
		ship.transform.position = here + ship.transform.forward * (_speed * dt) + slide;
		Write(Velocity, ship, ship.transform.forward * _speed);

		if (_speed > 0.05f || off > SettleAngle || gap.magnitude > SettleDistance)
		{
			return;
		}

		ship.transform.SetPositionAndRotation(new Vector3(_dock.x, here.y, _dock.z), Quaternion.LookRotation(_heading));
		_speed = 0f;
		Dock();
	}

	private void Dock()
	{
		var ship = _ship!;
		_phase = Phase.Hold;
		Write(Throttle, ship, 0f);
		Write(Turning, ship, 0f);
		Write(Velocity, ship, Vector3.zero);

		if (CargoShip.refresh_loot_on_dock)
		{
			ship.RespawnLoot();
		}

		PauseTimer();
		Log.Info("Cargo: ship docked, leaving in " + Mathf.RoundToInt(CargoShip.dock_time) + "s"
			+ (_timerPaused ? ", event timer paused with " + Mathf.RoundToInt(TimeLeft(ship)) + "s left" : string.Empty));
		Invoke(nameof(Horn), Mathf.Max(0f, CargoShip.dock_time - 60f));
		Invoke(nameof(Depart), CargoShip.dock_time);
	}

	private void Horn()
	{
		if (_ship != null && !_ship.IsDestroyed)
		{
			_ship.PlayHorn();
		}
	}

	private void Depart()
	{
		if (_ship == null || _ship.IsDestroyed)
		{
			Destroy(this);
			return;
		}

		using (var flags = _ship.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdateImmediate))
		{
			flags.Set(CargoShip.HasDocked, true);
		}

		ResumeTimer();
		_left = 0f;
		_phase = Phase.EaseOut;
	}

	private void PauseTimer()
	{
		var ship = _ship!;
		if (_timerPaused || !Settings.Current.CargoPauseWhileDocked || Lifetime == null || IsEgressing(ship))
		{
			return;
		}

		_egressDue = ship.IsInvoking(ship.StartEgress);
		if (_egressDue)
		{
			ship.CancelInvoke(ship.StartEgress);
		}

		_frozenLifetime = (float)Lifetime.GetValue(ship);
		_timerPaused = true;
	}

	private void ResumeTimer()
	{
		var ship = _ship;
		if (!_timerPaused || ship == null || ship.IsDestroyed)
		{
			return;
		}

		_timerPaused = false;
		Write(Lifetime, ship, _frozenLifetime);
		if (_egressDue && !IsEgressing(ship))
		{
			ship.Invoke(ship.StartEgress, Mathf.Max(0f, TimeLeft(ship)));
		}

		Log.Info("Cargo: ship left the dock, event timer running again with " + Mathf.RoundToInt(TimeLeft(ship)) + "s left");
	}

	private static float TimeLeft(CargoShip ship)
	{
		var lifetime = Lifetime?.GetValue(ship) is float value ? value : 0f;
		return CargoShip.event_duration_minutes * 60f - lifetime;
	}

	private void EaseAway(float dt)
	{
		var ship = _ship!;
		Move(EaseForward, dt);
		var step = Mathf.Min(EaseOutSpeed * dt, EaseOut - _left);
		ship.transform.position += _away * step;
		_left += step;

		if (_left < EaseOut)
		{
			return;
		}

		_node = Ahead();
		_left = 0f;
		_phase = Phase.Leave;
	}

	private int Ahead()
	{
		if (_path.Count == 0)
		{
			return -1;
		}

		var here = _ship!.transform.position;
		var target = here + _heading * 60f;
		var nearest = -1;
		var best = float.MaxValue;
		for (var i = 0; i < _path.Count; i++)
		{
			if (Vector3.Dot(Flat(_path[i] - here), _away) < -1f)
			{
				continue;
			}

			var gap = Flat(_path[i] - target).sqrMagnitude;
			if (gap < best)
			{
				best = gap;
				nearest = i;
			}
		}

		return nearest;
	}

	private void Leave(float dt)
	{
		var ship = _ship!;
		if (_node < 0)
		{
			Release(ship.targetNodeIndex);
			return;
		}

		var here = ship.transform.position;
		while (Flat(_path[_node] - here).magnitude < NodeReach)
		{
			_node = (_node - 1 + _path.Count) % _path.Count;
		}

		var off = Turn(Flat(_path[_node] - here), dt);
		Move(off > SlowAngle ? CreepSpeed : LeaveSpeed, dt);
		_left += _speed * dt;

		if (_left >= LeaveDistance && off < 5f)
		{
			Release(_node);
		}
	}

	private void Release(int node)
	{
		var ship = _ship!;
		ship.targetNodeIndex = node;
		Write(Throttle, ship, _speed / 8f);
		Write(Turning, ship, 0f);
		Driving.Remove(ship);
		Destroy(this);
	}

	private float Turn(Vector3 toward, float dt)
	{
		var ship = _ship!;
		var facing = Flat(ship.transform.forward).normalized;
		if (toward.sqrMagnitude < 0.01f)
		{
			return 0f;
		}

		var off = Vector3.SignedAngle(facing, toward.normalized, Vector3.up);
		var step = Mathf.Clamp(off, -TurnRate * dt, TurnRate * dt);
		ship.transform.rotation = Quaternion.AngleAxis(step, Vector3.up) * ship.transform.rotation;
		Write(Turning, ship, step / Mathf.Max(dt, 0.0001f));
		return Mathf.Abs(off - step);
	}

	private void Move(float wanted, float dt)
	{
		var ship = _ship!;
		_speed = Mathf.MoveTowards(_speed, wanted, Accel * dt);
		var velocity = Flat(ship.transform.forward).normalized * _speed;
		ship.transform.position += velocity * dt;
		Write(Throttle, ship, _speed / 8f);
		Write(Velocity, ship, velocity);
	}

	private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

	private static float Read(FieldInfo? field, CargoShip ship)
	{
		if (field == null)
		{
			Log.Once("cargo throttle", new Exception("CargoShip.currentThrottle or currentTurnSpeed is missing"));
			return 0f;
		}

		return (float)field.GetValue(ship);
	}

	private static void Write(FieldInfo? field, CargoShip ship, object value)
	{
		field?.SetValue(ship, value);
	}
}
