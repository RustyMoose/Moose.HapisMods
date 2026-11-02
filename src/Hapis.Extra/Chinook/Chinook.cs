using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moose.Hapis.Extra;

internal class DropRun
{
	public DropSpot Spot = null!;
	public CH47HelicopterAIController? Heli;
	public HackableLockedCrate? LastCrate;
	public float NextAt;

	public Vector3 Point => new(Spot.X, Spot.Y, Spot.Z);

	public bool Flying => Heli != null && !Heli.IsDestroyed;
}

internal static class Chinook
{
	public static readonly HashSet<BaseNetworkable> Managed = new();
	public static readonly List<DropRun> Runs = new();

	public static void Setup(float now)
	{
		Runs.Clear();
		foreach (var spot in Settings.Current.Drops)
		{
			if (spot == null)
			{
				continue;
			}

			Runs.Add(new DropRun { Spot = spot, NextAt = now + Mathf.Max(0f, spot.FirstHours) * 3600f });
		}
	}

	public static DropRun? Find(string? name)
	{
		if (Runs.Count == 0)
		{
			return null;
		}

		if (string.IsNullOrEmpty(name))
		{
			return Runs[0];
		}

		return Runs.Find(r => string.Equals(r.Spot.Name, name, StringComparison.OrdinalIgnoreCase));
	}

	public static bool Spawn(DropRun run, out string message)
	{
		if (run.Flying)
		{
			message = "A Chinook is already flying to " + run.Spot.Name + ".";
			return false;
		}

		if (TerrainMeta.HeightMap == null || World.Size == 0)
		{
			message = "World not ready yet.";
			return false;
		}

		var s = Settings.Current;
		var drop = run.Point;
		var start = EdgeStart(drop);
		start.y = ChinookRoute.PathGround(start, drop) + s.CruiseClearance;

		var look = drop - start;
		look.y = 0f;

		var heli = GameManager.server.CreateEntity(s.Prefab, start, Quaternion.LookRotation(look.normalized), true) as CH47HelicopterAIController;
		if (heli == null)
		{
			message = "Could not spawn '" + s.Prefab + "', check the Chinook Prefab setting.";
			return false;
		}

		Managed.Add(heli);
		heli.Spawn();
		run.Heli = heli;

		var brain = heli.GetComponent<CH47AIBrain>();
		if (brain != null)
		{
			brain.SetEnabled(false);
			brain.enabled = false;
		}

		heli.gameObject.AddComponent<ChinookRoute>().Init(heli, run);

		message = "Chinook heading to " + run.Spot.Name;
		return true;
	}

	public static void DespawnAll()
	{
		foreach (var run in Runs)
		{
			Despawn(run);
		}
	}

	public static void Despawn(DropRun run)
	{
		var heli = run.Heli;
		run.Heli = null;
		if (heli == null)
		{
			return;
		}

		Managed.Remove(heli);
		if (!heli.IsDestroyed)
		{
			heli.Kill();
		}
	}

	public static void Forget(DropRun run, CH47HelicopterAIController? heli)
	{
		if (heli != null)
		{
			Managed.Remove(heli);
		}

		if (run.Heli == heli)
		{
			run.Heli = null;
		}
	}

	public static void DropCrate(DropRun run, CH47HelicopterAIController heli)
	{
		ClearOldCrate(run);

		var drop = run.Point;
		var floor = Mathf.Max(drop.y, ChinookRoute.Ground(drop)) + 2f;
		var spot = new Vector3(drop.x, Mathf.Max(heli.transform.position.y - 5f, floor), drop.z);
		var turn = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
		var crate = GameManager.server.CreateEntity(heli.lockedCratePrefab.resourcePath, spot, turn, true) as HackableLockedCrate;
		if (crate == null)
		{
			Log.Info("Chinook could not create its crate for " + run.Spot.Name);
			return;
		}

		crate.SetWasDropped();
		crate.Spawn();
		run.LastCrate = crate;
		Log.Info("Crate dropped at " + run.Spot.Name);
	}

	private static void ClearOldCrate(DropRun run)
	{
		var old = run.LastCrate;
		run.LastCrate = null;
		if (!Settings.Current.RemoveOldCrate || old == null || old.IsDestroyed || old.IsBeingHacked() || old.IsFullyHacked())
		{
			return;
		}

		old.Kill();
		Log.Info("Removed the last crate at " + run.Spot.Name + ", nobody hacked it.");
	}

	private static Vector3 EdgeStart(Vector3 drop)
	{
		var half = World.Size * 0.5f;
		var flatDrop = new Vector3(drop.x, 0f, drop.z);
		var start = Vector3.zero;
		for (var i = 0; i < 10; i++)
		{
			var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
			start = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (half - 60f);
			if ((start - flatDrop).magnitude > 400f)
			{
				break;
			}
		}

		return start;
	}
}


internal class ChinookRoute : MonoBehaviour
{
	private const float Beat = 0.25f;
	private const float StartDropWithin = 25f;
	private const float DropWithin = 4f;
	private const float SettleSeconds = 3f;
	private const float GiveUpSeconds = 90f;
	private const float ApproachLimitSeconds = 600f;

	private enum Leg { Approach, Drop, Circle, Leave }

	private CH47HelicopterAIController? _heli;
	private DropRun _run = null!;
	private Vector3 _drop;
	private float _half;
	private Leg _leg;
	private float _legStart;
	private float _nextBeat;
	private float _droppedAt = -1f;

	public void Init(CH47HelicopterAIController heli, DropRun run)
	{
		_heli = heli;
		_run = run;
		_drop = run.Point;
		_half = World.Size * 0.5f;
		Enter(Leg.Approach);
	}

	public static float Ground(Vector3 at)
	{
		var height = 0f;
		try
		{
			if (TerrainMeta.HeightMap != null)
			{
				height = TerrainMeta.HeightMap.GetHeight(at);
			}
		}
		catch (Exception)
		{
		}

		return Mathf.Max(height, 0f);
	}

	public static float PathGround(Vector3 from, Vector3 to)
	{
		var highest = 0f;
		for (var i = 0; i <= 4; i++)
		{
			highest = Mathf.Max(highest, Ground(Vector3.Lerp(from, to, i / 4f)));
		}

		return highest;
	}

	private static Vector3 Flat(Vector3 v)
	{
		v.y = 0f;
		return v;
	}

	private float HoverY => Mathf.Max(_drop.y, Ground(_drop)) + Settings.Current.DropClearance;

	private void Enter(Leg leg)
	{
		_leg = leg;
		_legStart = Time.time;
		var s = Settings.Current;
		_heli?.SetMinHoverHeight(leg == Leg.Drop ? s.DropClearance - 5f : s.CruiseClearance - 10f);
	}

	private void Update()
	{
		var heli = _heli;
		if (heli == null || heli.IsDestroyed)
		{
			Chinook.Forget(_run, heli);
			Destroy(this);
			return;
		}

		if (Time.time < _nextBeat)
		{
			return;
		}

		_nextBeat = Time.time + Beat;

		if (Settings.Current.Invulnerable && heli.health < heli.MaxHealth())
		{
			heli.SetHealth(heli.MaxHealth());
		}

		var at = heli.transform.position;
		var elapsed = Time.time - _legStart;
		switch (_leg)
		{
			case Leg.Approach:
				Approach(heli, at, elapsed);
				break;
			case Leg.Drop:
				Drop(heli, at, elapsed);
				break;
			case Leg.Circle:
				Circle(heli, at, elapsed);
				break;
			case Leg.Leave:
				Leave(heli, at, elapsed);
				break;
		}
	}

	private void Approach(CH47HelicopterAIController heli, Vector3 at, float elapsed)
	{
		var toDrop = Flat(_drop - at);
		var distance = toDrop.magnitude;
		if (distance < StartDropWithin || elapsed > ApproachLimitSeconds)
		{
			Enter(Leg.Drop);
			return;
		}

		var next = at + toDrop.normalized * Mathf.Min(distance, 200f);
		next.y = distance < 120f ? HoverY : PathGround(at, next) + Settings.Current.CruiseClearance;
		heli.SetMoveTarget(next);
	}

	private void Drop(CH47HelicopterAIController heli, Vector3 at, float elapsed)
	{
		heli.SetMoveTarget(new Vector3(_drop.x, HoverY, _drop.z));

		if (_droppedAt < 0f)
		{
			if (elapsed < 0.5f)
			{
				heli.SetDropDoorOpen(true);
			}

			var over = Flat(_drop - at).magnitude <= DropWithin;
			if ((over && elapsed >= SettleSeconds) || elapsed >= GiveUpSeconds)
			{
				Chinook.DropCrate(_run, heli);
				_droppedAt = Time.time;
			}

			return;
		}

		if (Time.time - _droppedAt >= 5f)
		{
			heli.SetDropDoorOpen(false);
			Enter(Leg.Circle);
		}
	}

	private void Circle(CH47HelicopterAIController heli, Vector3 at, float elapsed)
	{
		var s = Settings.Current;
		if (elapsed >= s.OrbitSeconds)
		{
			Enter(Leg.Leave);
			return;
		}

		var offset = Flat(at - _drop);
		var angle = Mathf.Atan2(offset.z, offset.x) + 35f * Mathf.Deg2Rad;
		var next = _drop + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * s.OrbitRadius;
		next.y = PathGround(at, next) + s.CruiseClearance;
		heli.SetMoveTarget(next);
	}

	private void Leave(CH47HelicopterAIController heli, Vector3 at, float elapsed)
	{
		var s = Settings.Current;
		if (Flat(at).magnitude > _half - 30f || elapsed > s.EgressTimeoutSeconds)
		{
			Chinook.Despawn(_run);
			Destroy(this);
			return;
		}

		var away = Flat(_drop);
		away = away.magnitude < 1f ? Vector3.right : away.normalized;
		var far = away * (_half + 400f);
		var toFar = Flat(far - at);
		var next = at + toFar.normalized * Mathf.Min(toFar.magnitude, 200f);
		next.y = PathGround(at, next) + s.CruiseClearance;
		heli.SetMoveTarget(next);
	}
}
