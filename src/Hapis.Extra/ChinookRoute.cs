using System;
using UnityEngine;

namespace Moose.Hapis.Extra;

/// <summary>
/// The flight route: Approach -> Drop -> Orbit -> Egress -> despawn.
/// </summary>
public class ChinookRoute : MonoBehaviour
{
	private enum State { Approach, Drop, Orbit, Egress }

	private CH47HelicopterAIController? _ent;
	private Vector3 _dropPos;
	private Vector3 _center = Vector3.zero;
	private float _half;

	private State _state;
	private float _stateStart;
	private float _nextTick;
	private bool _doorOpened;
	private bool _crateDropped;

	public void Init(CH47HelicopterAIController e, Vector3 drop)
	{
		_ent = e;
		_dropPos = drop;
		_half = (float)World.Size * 0.5f;
		Enter(State.Approach);
	}

	// helpers

	private static Vector3 Flat(Vector3 v)
	{
		v.y = 0f;
		return v;
	}

	public static float Ground(Vector3 p)
	{
		var h = 0f;
		try
		{
			if (TerrainMeta.HeightMap != null)
			{
				h = TerrainMeta.HeightMap.GetHeight(p);
			}
		}
		catch (Exception)
		{
		}
		return Mathf.Max(h, 0f);
	}

	public static float PathGround(Vector3 from, Vector3 to)
	{
		var g = 0f;
		for (var i = 0; i <= 4; i++)
		{
			g = Mathf.Max(g, Ground(Vector3.Lerp(from, to, i / 4f)));
		}
		return g;
	}

	private void MoveTo(Vector3 target)
	{
		_ent?.SetMoveTarget(target);
	}

	private void Enter(State s)
	{
		_state = s;
		_stateStart = Time.time;
		var hover = (s == State.Drop) ? HapisExtraConstants.DropClearance - 5f : HapisExtraConstants.CruiseClearance - 10f;
		_ent?.SetMinHoverHeight(hover);
		HapisExtraMod.Log("State -> " + s);
	}

	// main loop

	private void Update()
	{
		if (_ent == null || _ent.IsDestroyed)
		{
			HapisExtraMod.Managed.Remove(_ent);
			Destroy(this);
			return;
		}
		if (Time.time < _nextTick)
		{
			return;
		}
		_nextTick = Time.time + 0.25f;

		if (HapisExtraConstants.Invulnerable && _ent.health < _ent.MaxHealth())
		{
			_ent.SetHealth(_ent.MaxHealth());
		}

		var pos = _ent.transform.position;
		var elapsed = Time.time - _stateStart;

		switch (_state)
		{
			case State.Approach:
				TickApproach(pos, elapsed);
				break;
			case State.Drop:
				TickDrop(pos, elapsed);
				break;
			case State.Orbit:
				TickOrbit(pos, elapsed);
				break;
			case State.Egress:
				TickEgress(pos, elapsed);
				break;
		}
	}

	private void TickApproach(Vector3 pos, float elapsed)
	{
		var toDrop = Flat(_dropPos - pos);
		var dist = toDrop.magnitude;

		if (dist < 25f || elapsed > 150f)
		{
			Enter(State.Drop);
			return;
		}

		var wp = pos + toDrop.normalized * Mathf.Min(dist, 200f);
		float y;
		if (dist < 120f)
		{
			y = Mathf.Max(_dropPos.y, Ground(_dropPos)) + HapisExtraConstants.DropClearance;
		}
		else
		{
			y = PathGround(pos, wp) + HapisExtraConstants.CruiseClearance;
		}
		wp.y = y;
		MoveTo(wp);
	}

	private void TickDrop(Vector3 pos, float elapsed)
	{
		var hold = new Vector3(_dropPos.x, Mathf.Max(_dropPos.y, Ground(_dropPos)) + HapisExtraConstants.DropClearance, _dropPos.z);
		MoveTo(hold);

		var dist = Flat(_dropPos - pos).magnitude;

		if (!_doorOpened)
		{
			_ent?.SetDropDoorOpen(true);
			_doorOpened = true;
		}

		if (!_crateDropped && _ent != null && elapsed >= 5f && (dist < 35f || elapsed > 40f))
		{
			_ent.numCrates = 1;
			_ent.DropCrate();
			_crateDropped = true;
			HapisExtraMod.Log("Crate drop called.");
		}

		if (_crateDropped && elapsed >= 10f)
		{
			_ent?.SetDropDoorOpen(false);
			Enter(State.Orbit);
		}
	}

	private void TickOrbit(Vector3 pos, float elapsed)
	{
		if (elapsed >= HapisExtraConstants.OrbitSeconds)
		{
			Enter(State.Egress);
			return;
		}

		var rel = Flat(pos - _dropPos);
		var ang = Mathf.Atan2(rel.z, rel.x) + 35f * Mathf.Deg2Rad;
		var target = _dropPos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * HapisExtraConstants.OrbitRadius;
		target.y = PathGround(pos, target) + HapisExtraConstants.CruiseClearance;
		MoveTo(target);
	}

	private void TickEgress(Vector3 pos, float elapsed)
	{
		var outDir = Flat(_dropPos - _center);
		if (outDir.magnitude < 1f)
		{
			outDir = Vector3.right;
		}
		outDir.Normalize();

		var far = _center + outDir * (_half + 400f);
		far.y = 0f;

		var fromCenter = Flat(pos - _center).magnitude;
		if (fromCenter > _half - 30f || elapsed > HapisExtraConstants.EgressTimeoutSeconds)
		{
			HapisExtraMod.Log("Egress complete, despawning.");
			HapisExtraMod.Despawn(_ent);
			Destroy(this);
			return;
		}

		var toFar = Flat(far - pos);
		var wp = pos + toFar.normalized * Mathf.Min(toFar.magnitude, 200f);
		wp.y = PathGround(pos, wp) + HapisExtraConstants.CruiseClearance;
		MoveTo(wp);
	}
}
