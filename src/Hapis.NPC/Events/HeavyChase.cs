using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Moose.Hapis.NPC.Behaviours;
using UnityEngine;

namespace Moose.Hapis.NPC.Events;

internal sealed class HeavyChase : MonoBehaviour
{
	private const float TickSeconds = 1f;
	private const float ChaseRange = 80f;
	private const float RevengeSeconds = 15f;
	private const float WakeDistance = 150f;
	private const float RoamRadius = 25f;
	private const float SnapReach = 6f;
	private const float StuckSeconds = 8f;
	private const int RoamTries = 5;
	private const int PlayerLayer = 131072;
	private const float LevelGap = 1.5f;
	private const float FlightReach = 40f;
	private const float BoardDistance = 3f;
	private const float RunSpeed = 4.5f;
	private const float WalkSpeed = 2f;
	private const float LookSeconds = 60f;
	private const float RoamPauseMin = 4f;
	private const float RoamPauseMax = 8f;
	private const float StairChance = 0.35f;
	private const float Tether = 60f;
	private const float StrideSeconds = 3f;
	private const float StrideStop = 2.5f;
	private const float MeshReach = 1.5f;
	private const int FloorLayers = (1 << 0) | (1 << 16) | (1 << 21);
	private const int WallLayers = (1 << 0) | (1 << 8) | (1 << 16) | (1 << 21);

	private static readonly HashSet<BaseEntity> Heavies = new();
	private static bool _noPathTold;

	private HumanNPC _npc = null!;
	private bool _landed;
	private Vector3 _home;
	private Vector3 _lastSpot;
	private float _stuckSince;
	private bool _onStairs;
	private bool _offMesh;
	private bool _hurry;
	private float _nextLook;
	private float _nextRoam;

	private BaseNavigator.NavigationSpeed Pace => _hurry ? BaseNavigator.NavigationSpeed.Fast : BaseNavigator.NavigationSpeed.Normal;

	private float Speed => _hurry ? RunSpeed : WalkSpeed;

	public void Begin(HumanNPC npc)
	{
		_npc = npc;
		InvokeRepeating(nameof(Tick), TickSeconds, TickSeconds);
	}

	public static bool Owns(BaseEntity? npc) => npc != null && Heavies.Contains(npc);

	private void OnDestroy()
	{
		if (_npc != null)
		{
			Heavies.Remove(_npc);
		}
	}

	private static void Sharpen(BaseAIBrain brain)
	{
		var config = HapisNPC.Instance?.Config;
		if (config == null)
		{
			return;
		}

		brain.SenseRange = Mathf.Max(brain.SenseRange, config.HeavyScientistNPCSenseRange);
		brain.TargetLostRange = Mathf.Max(brain.TargetLostRange, config.HeavyScientistNPCTargetLostRange);
		brain.MemoryDuration = Mathf.Max(brain.MemoryDuration, config.HeavyScientistNPCMemoryDuration);
	}

	private static bool PlayersNear(Vector3 spot, float radius)
	{
		var found = Facepunch.Pool.Get<List<BasePlayer>>();
		Vis.Entities(spot, radius, found, PlayerLayer);

		var any = false;
		foreach (var player in found)
		{
			if (player != null && !player.IsNpc && player.IsConnected && player.IsAlive())
			{
				any = true;
				break;
			}
		}

		Facepunch.Pool.FreeUnmanaged(ref found);
		return any;
	}

	private void Land(BaseNavigator navigator)
	{
		var spot = _npc.transform.position;
		if (navigator.GetNearestNavmeshPosition(spot, out var walkable, SnapReach))
		{
			spot = walkable;
		}

		navigator.Stop();
		_npc.ServerPosition = spot;
		HapisNPC.Instance?.SnapEntityToFloor(_npc);
		navigator.SetNavMeshEnabled(true);
		navigator.Agent?.Warp(_npc.transform.position);
		_npc.SendNetworkUpdateImmediate();

		_lastSpot = _npc.transform.position;
		_stuckSince = 0f;
	}

	private void Unstick(BaseNavigator navigator)
	{
		var spot = _npc.transform.position;
		if (!navigator.Moving || (spot - _lastSpot).sqrMagnitude > 0.25f)
		{
			_lastSpot = spot;
			_stuckSince = 0f;
			return;
		}

		if (_stuckSince == 0f)
		{
			_stuckSince = Time.time;
			return;
		}

		if (Time.time - _stuckSince >= StuckSeconds)
		{
			Land(navigator);
		}
	}

	private void Wander(BaseNavigator navigator)
	{
		if (navigator.Moving || Time.time < _nextRoam)
		{
			return;
		}

		_nextRoam = Time.time + UnityEngine.Random.Range(RoamPauseMin, RoamPauseMax);
		_hurry = false;

		var here = _npc.transform.position;
		if (Vector3.Distance(here, _home) > Tether)
		{
			Head(navigator, _home);
			return;
		}

		if (UnityEngine.Random.value < StairChance && OilRigStairs.Any(here, LevelGap, RoamRadius, out _, out var other))
		{
			Head(navigator, other);
			return;
		}

		for (var i = 0; i < RoamTries; i++)
		{
			var circle = UnityEngine.Random.insideUnitCircle * RoamRadius;
			var spot = here + new Vector3(circle.x, 0f, circle.y);
			if (navigator.GetNearestNavmeshPosition(spot, out var walkable, 4f) && Vector3.Distance(walkable, _home) <= Tether && navigator.SetDestination(walkable, BaseNavigator.NavigationSpeed.Slow, 0f, 0f))
			{
				return;
			}
		}

		var stray = UnityEngine.Random.insideUnitCircle.normalized * RoamRadius;
		StartCoroutine(Stride(navigator, here + new Vector3(stray.x, 0f, stray.y)));
	}

	private void Tick()
	{
		try
		{
			if (_npc == null || _npc.IsDestroyed || _npc.IsDead())
			{
				Destroy(this);
				return;
			}

			if (_npc.isMounted || _onStairs)
			{
				return;
			}

			var brain = _npc.Brain;
			if (brain == null || brain.Navigator == null)
			{
				return;
			}

			var navigator = brain.Navigator;
			if (!_landed)
			{
				_landed = true;
				_home = _npc.transform.position;
				Heavies.Add(_npc);
				Sharpen(brain);
				Land(navigator);
			}
			else if (!_offMesh && (navigator.Agent == null || !navigator.Agent.isOnNavMesh))
			{
				Land(navigator);
			}

			HapisNPC.Instance?.SnapStandingNpc(_npc);

			if (!PlayersNear(_npc.transform.position, WakeDistance))
			{
				return;
			}

			((IAISleepable)brain).WakeAI();
			Unstick(navigator);

			var target = Target(brain);
			if (target == null || Vector3.Distance(target.transform.position, _npc.transform.position) > ChaseRange)
			{
				if (_offMesh)
				{
					Settle(navigator);
				}

				Look(navigator);
				if (!_onStairs)
				{
					Wander(navigator);
				}

				return;
			}

			brain.Events.Memory.Entity.Set(target, 0);
			_hurry = true;
			Head(navigator, target.transform.position);
		}
		catch (Exception e)
		{
			HapisLog.Once("CH47 heavy chase", e);
		}
	}

	private void Look(BaseNavigator navigator)
	{
		if (Time.time < _nextLook)
		{
			return;
		}

		_nextLook = Time.time + LookSeconds;
		var player = NearestPlayer();
		if (player == null)
		{
			return;
		}

		_hurry = false;
		Head(navigator, player.transform.position);
	}

	private void Head(BaseNavigator navigator, Vector3 goal)
	{
		if (Mathf.Abs(goal.y - _npc.transform.position.y) > LevelGap && TakeStairs(navigator, goal))
		{
			return;
		}

		var spot = goal;
		if (navigator.GetNearestNavmeshPosition(spot, out var walkable, 4f))
		{
			spot = walkable;
		}

		if (!_offMesh && navigator.SetDestination(spot, Pace, 0f, 0f))
		{
			return;
		}

		if (Mathf.Abs(goal.y - _npc.transform.position.y) <= LevelGap)
		{
			StartCoroutine(Stride(navigator, goal));
			return;
		}

		if (_noPathTold)
		{
			return;
		}

		_noPathTold = true;
		HapisLog.Info("CH47 Heavies: no path from " + _npc.transform.position + " to " + spot + " and no stairs to run");
	}

	private IEnumerator Stride(BaseNavigator navigator, Vector3 goal)
	{
		_onStairs = true;
		navigator.Stop();
		navigator.SetNavMeshEnabled(false);

		var until = Time.time + StrideSeconds;
		while (Time.time < until && _npc != null && !_npc.IsDestroyed && !_npc.IsDead())
		{
			var here = _npc.transform.position;
			var flat = goal - here;
			flat.y = 0f;
			if (flat.magnitude <= StrideStop)
			{
				break;
			}

			var dir = flat.normalized;
			if (Physics.SphereCast(here + Vector3.up, 0.3f, dir, out _, 0.6f, WallLayers, QueryTriggerInteraction.Ignore))
			{
				break;
			}

			var next = here + dir * (Speed * Time.deltaTime);
			if (!Physics.Raycast(next + Vector3.up * 0.5f, Vector3.down, out var floor, 1f, FloorLayers, QueryTriggerInteraction.Ignore))
			{
				break;
			}

			next.y = floor.point.y;
			_npc.ServerPosition = next;
			_npc.ServerRotation = Quaternion.LookRotation(dir);
			_npc.SendNetworkUpdate();
			yield return null;
		}

		_onStairs = false;
		if (_npc == null || _npc.IsDestroyed || _npc.IsDead())
		{
			yield break;
		}

		Settle(navigator);
	}

	private void Settle(BaseNavigator navigator)
	{
		if (navigator.GetNearestNavmeshPosition(_npc.transform.position, out var walkable, MeshReach))
		{
			_offMesh = false;
			Land(navigator);
			return;
		}

		_offMesh = true;
		_npc.SendNetworkUpdateImmediate();
	}

	private bool TakeStairs(BaseNavigator navigator, Vector3 goal)
	{
		OilRigStairs.Ensure();
		var here = _npc.transform.position;
		if (!OilRigStairs.Pick(here, goal, LevelGap, FlightReach, out var from, out var to))
		{
			return false;
		}

		var gap = from - here;
		gap.y = 0f;
		if (gap.magnitude <= BoardDistance)
		{
			StartCoroutine(RunStairs(navigator, from, to));
			return true;
		}

		var board = from;
		if (navigator.GetNearestNavmeshPosition(from, out var walkable, 2f))
		{
			board = walkable;
		}

		if (!_offMesh && navigator.SetDestination(board, Pace, 0f, 0f))
		{
			return true;
		}

		StartCoroutine(Stride(navigator, from));
		return true;
	}

	private IEnumerator RunStairs(BaseNavigator navigator, Vector3 from, Vector3 to)
	{
		_onStairs = true;
		navigator.Stop();
		navigator.SetNavMeshEnabled(false);

		var flat = to - from;
		flat.y = 0f;
		var facing = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : _npc.transform.rotation;
		var step = flat.normalized * OilRigStairs.StepOff;
		var path = new[] { from, from + step, to - step, to };

		for (var i = 1; i < path.Length; i++)
		{
			var start = path[i - 1];
			var end = path[i];
			var length = Vector3.Distance(start, end);
			var travelled = 0f;
			while (travelled < length)
			{
				if (_npc == null || _npc.IsDestroyed || _npc.IsDead())
				{
					_onStairs = false;
					yield break;
				}

				travelled = Mathf.Min(length, travelled + Speed * Time.deltaTime);
				_npc.ServerPosition = Vector3.Lerp(start, end, length > 0f ? travelled / length : 1f);
				_npc.ServerRotation = facing;
				_npc.SendNetworkUpdate();
				yield return null;
			}
		}

		_onStairs = false;
		Settle(navigator);
	}

	private BaseEntity? Target(BaseAIBrain brain)
	{
		if (_npc.lastAttacker is BasePlayer attacker && !attacker.IsDead() && Time.time - _npc.lastAttackedTime < RevengeSeconds)
		{
			return attacker;
		}

		return brain.Senses?.GetNearestTarget(ChaseRange);
	}

	private BasePlayer? NearestPlayer()
	{
		var found = Facepunch.Pool.Get<List<BasePlayer>>();
		Vis.Entities(_npc.transform.position, ChaseRange, found, PlayerLayer);

		BasePlayer? best = null;
		var bestDistance = float.MaxValue;
		foreach (var player in found)
		{
			if (player == null || player.IsNpc || !player.IsConnected || !player.IsAlive() || player.IsSleeping() || player.InSafeZone())
			{
				continue;
			}

			var distance = (player.transform.position - _npc.transform.position).sqrMagnitude;
			if (distance < bestDistance)
			{
				best = player;
				bestDistance = distance;
			}
		}

		Facepunch.Pool.FreeUnmanaged(ref found);
		return best;
	}
}

[HarmonyPatch]
internal static class CH47HeavySpawnPatch
{
	private static MethodBase? _target;
	private static bool _announced;

	private static bool Prepare()
	{
		_target ??= AccessTools.Method(typeof(CH47HelicopterAIController), "OnSpawnedHuman", new[] { typeof(HumanNPC) });
		if (_target == null && !_announced)
		{
			_announced = true;
			HapisLog.Info("CH47 Events: heavy spawn hook missing - CH47 heavies use Rust's own behaviour");
		}

		return _target != null;
	}

	private static MethodBase TargetMethod() => _target!;

	[HarmonyPostfix]
	private static void Postfix(CH47HelicopterAIController __instance, HumanNPC __0)
	{
		try
		{
			if (__0 != null && CH47Events.IsOurs(__instance))
			{
				__0.gameObject.AddComponent<HeavyChase>().Begin(__0);
			}
		}
		catch (Exception e)
		{
			HapisLog.Once("CH47 heavy spawn", e);
		}
	}
}
