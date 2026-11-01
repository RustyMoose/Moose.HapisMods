using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Moose.Hapis.NPC.Behaviours;

internal static class NpcCover
{
	private const int CoverLayers = (1 << 0) | (1 << 8) | (1 << 16) | (1 << 21) | (1 << 23);
	private const float Step = 3f;
	private const float ExtraRadius = 12f;
	private const float MinRadius = 15f;
	private const float MaxRadius = 45f;
	private const float WallReach = 1.6f;
	private const float StandOff = 0.7f;
	private const float SameSpot = 1.5f;
	private const float CoverDot = 0.5f;
	private const float MaxCoverRange = 12f;
	private const float StrafeDistance = 4f;
	private const float ArriveDistance = 0.8f;
	private const float SettleDistance = 1.5f;
	private const float HitWindow = 2f;
	private const float ReactWindow = 4f;
	private const float UseSeconds = 15f;
	private const float StayMin = 4f;
	private const float StayMax = 8f;
	private const float Cooldown = 3f;
	private const int MaxPoints = 400;
	private const float PulseSeconds = 0.5f;
	private const float DownMin = 1.2f;
	private const float DownMax = 2.2f;
	private const float UpMin = 1f;
	private const float UpMax = 1.8f;
	private const float StandingEyes = 1.6f;
	private const float RetreatDistance = 8f;
	private const float CrouchHead = 1.15f;
	private const float CrouchChest = 0.7f;

	private const int MinListedSpots = 3;
	private const float AreaRetrySeconds = 15f;
	private const float MinCoverHeight = 1f;
	private const float ListedSpacing = 1.2f;

	private static readonly string[] CoverPrefabs =
	{
		"covera", "coverb", "coverc", "coverd", "covere",
		"cargopack", "washingmachine", "lockera", "lockerb",
		"tirestack", "largeindustrialprop", "trainwagon",
		"barricadesandbag", "barricadeconcrete", "woodencrate", "rubblepile"
	};

	private static readonly string[] SandbagPrefabs =
	{
		"sandbag"
	};

	private static readonly Vector3[] Directions =
	{
		new(1f, 0f, 0f), new(-1f, 0f, 0f), new(0f, 0f, 1f), new(0f, 0f, -1f),
		new Vector3(1f, 0f, 1f).normalized, new Vector3(1f, 0f, -1f).normalized,
		new Vector3(-1f, 0f, 1f).normalized, new Vector3(-1f, 0f, -1f).normalized
	};

	private static readonly Dictionary<Vector3Int, List<Spot>> Areas = new();
	private static readonly Dictionary<BaseEntity, Tactic> Tactics = new();
	private static readonly List<BaseEntity> Gone = new();

	private static MethodInfo? _bind;
	private static bool _bindLooked;

	private sealed class Spot
	{
		public Vector3 Position;
		public Vector3 Forward;
		public BaseEntity? User;
		public float UsedUntil;

		public bool InUse => User != null && Time.time < UsedUntil;
	}

	private enum Mode
	{
		None,
		Moving,
		InCover
	}

	private sealed class Tactic
	{
		public Mode Mode;
		public Spot? Spot;
		public List<Spot>? Area;
		public BaseEntity? Target;
		public Action? Pulse;
		public float LeaveAt;
		public float NextTry;
		public float RetreatUntil;
		public float AreaRetryAt;
		public bool Up;
		public float SwitchAt;
		public bool NextTryOverride;
	}

	public static bool Handle(NpcSpawner spawner, ScientistNPC npc, BaseEntity target)
	{
		var brain = npc.Brain;
		var navigator = brain != null ? brain.Navigator : null;
		if (brain == null || navigator == null || target == null)
		{
			return false;
		}

		Prune();

		if (!Tactics.TryGetValue(npc, out var tactic))
		{
			tactic = new Tactic();
			Tactics[npc] = tactic;
		}

		if ((tactic.Area == null || tactic.Area.Count == 0) && Time.time >= tactic.AreaRetryAt)
		{
			tactic.AreaRetryAt = Time.time + AreaRetrySeconds;
			tactic.Area = AreaFor(spawner, navigator);
		}

		tactic.Area ??= new List<Spot>();
		tactic.Target = target;
		var now = Time.time;

		if (tactic.Mode != Mode.None)
		{
			if (tactic.Mode == Mode.Moving || !tactic.Up)
			{
				HoldFire(brain, npc);
			}

			return true;
		}

		if (now < tactic.RetreatUntil && navigator.Moving)
		{
			return true;
		}

		var justHit = npc.SecondsSinceAttacked < HitWindow;
		var underFire = npc.SecondsSinceAttacked < ReactWindow;
		if ((now < tactic.NextTry && !underFire) || !WantsCover(npc, brain))
		{
			return false;
		}

		tactic.NextTry = now + 1f;

		var current = npc.transform.position;
		var hideFrom = target.transform.position;
		var eyes = EyesOf(target);
		var minRange = justHit ? 2f : 0f;
		var maxRange = navigator.BestCoverPointMaxDistance > 0f ? Mathf.Min(navigator.BestCoverPointMaxDistance, MaxCoverRange) : MaxCoverRange;
		var spot = Best(tactic.Area, current, hideFrom, eyes, minRange, maxRange, npc);

		WakeNavigator(navigator);

		if (spot == null || !navigator.SetDestination(spot.Position, BaseNavigator.NavigationSpeed.Fast, 0f, 0f))
		{
			if (npc.healthFraction <= brain.HealBelowHealthFraction && Retreat(npc, navigator, tactic, current, hideFrom))
			{
				return true;
			}

			return underFire && Strafe(navigator, tactic, current, hideFrom);
		}

		navigator.SetFacingDirectionEntity(target);
		spot.User = npc;
		spot.UsedUntil = now + UseSeconds;
		tactic.Spot = spot;
		tactic.Mode = Mode.Moving;
		StartPulse(npc, tactic);
		return true;
	}

	public static void Release(BaseEntity? npc)
	{
		if (npc == null || !Tactics.TryGetValue(npc, out var tactic))
		{
			return;
		}

		Leave(npc as ScientistNPC, tactic);
	}

	private static bool Retreat(ScientistNPC npc, BaseNavigator navigator, Tactic tactic, Vector3 current, Vector3 threat)
	{
		var away = current - threat;
		away.y = 0f;
		if (away.sqrMagnitude < 0.01f)
		{
			return false;
		}

		away.Normalize();
		for (var i = 0; i < 4; i++)
		{
			var turn = Quaternion.Euler(0f, (i % 2 == 0 ? 1f : -1f) * i * 25f, 0f);
			var spot = current + turn * away * RetreatDistance;
			if (navigator.GetNearestNavmeshPosition(spot, out var walkable, 3f) && navigator.SetDestination(walkable, BaseNavigator.NavigationSpeed.Fast, 0f, 0f))
			{
				navigator.SetFacingDirectionEntity(tactic.Target);
				tactic.RetreatUntil = Time.time + 3f;
				return true;
			}
		}

		return false;
	}

	private static bool Strafe(BaseNavigator navigator, Tactic tactic, Vector3 current, Vector3 threat)
	{
		var toThreat = threat - current;
		toThreat.y = 0f;
		if (toThreat.sqrMagnitude < 0.01f)
		{
			return false;
		}

		var side = Vector3.Cross(Vector3.up, toThreat.normalized);
		if (UnityEngine.Random.Range(0, 2) == 0)
		{
			side = -side;
		}

		for (var i = 0; i < 2; i++)
		{
			var spot = current + side * StrafeDistance * (i == 0 ? 1f : -1f);
			if (navigator.GetNearestNavmeshPosition(spot, out var walkable, 2f) && navigator.SetDestination(walkable, BaseNavigator.NavigationSpeed.Normal, 0f, 0f))
			{
				navigator.SetFacingDirectionEntity(tactic.Target);
				tactic.RetreatUntil = Time.time + 1.5f;
				return true;
			}
		}

		return false;
	}

	private static void StartPulse(ScientistNPC npc, Tactic tactic)
	{
		if (tactic.Pulse == null)
		{
			tactic.Pulse = () => Beat(npc, tactic);
		}

		npc.CancelInvoke(tactic.Pulse);
		npc.InvokeRepeating(tactic.Pulse, PulseSeconds, PulseSeconds);
	}

	private static void Beat(ScientistNPC npc, Tactic tactic)
	{
		if (npc == null || npc.IsDestroyed || npc.IsDead())
		{
			if (tactic.Pulse != null && npc != null)
			{
				npc.CancelInvoke(tactic.Pulse);
			}

			return;
		}

		var brain = npc.Brain;
		var navigator = brain != null ? brain.Navigator : null;
		var target = tactic.Target;
		if (brain == null || navigator == null || target == null || target.IsDestroyed || tactic.Spot == null)
		{
			Leave(npc, tactic);
			return;
		}

		var now = Time.time;

		if (tactic.Mode == Mode.Moving)
		{
			HoldFire(brain, npc);
			navigator.SetFacingDirectionEntity(target);

			var gap = Vector3.Distance(npc.transform.position, tactic.Spot.Position);
			var settled = !navigator.Moving && gap <= SettleDistance && Safe(npc.transform.position, tactic.Spot, target);
			if (gap <= ArriveDistance || settled)
			{
				tactic.Mode = Mode.InCover;
				tactic.LeaveAt = now + UnityEngine.Random.Range(StayMin, StayMax);
				tactic.Spot.User = npc;
				tactic.Spot.UsedUntil = tactic.LeaveAt + 1f;
				tactic.Up = false;
				tactic.SwitchAt = now + UnityEngine.Random.Range(DownMin, DownMax);
				navigator.Stop();
				npc.SetDucked(true);
				return;
			}

			if (!navigator.Moving)
			{
				Leave(npc, tactic);
			}

			return;
		}

		if (now >= tactic.LeaveAt || !Safe(npc.transform.position, tactic.Spot, target))
		{
			if (now < tactic.LeaveAt)
			{
				tactic.NextTryOverride = true;
			}

			Leave(npc, tactic);
			if (tactic.NextTryOverride)
			{
				tactic.NextTry = 0f;
				tactic.NextTryOverride = false;
			}

			return;
		}

		if (Vector3.Distance(npc.transform.position, tactic.Spot.Position) > ArriveDistance)
		{
			navigator.SetDestination(tactic.Spot.Position, BaseNavigator.NavigationSpeed.Fast, 0f, 0f);
		}
		else if (navigator.Moving)
		{
			navigator.Stop();
		}

		navigator.SetFacingDirectionEntity(target);

		if (now >= tactic.SwitchAt)
		{
			tactic.Up = !tactic.Up;
			tactic.SwitchAt = now + (tactic.Up ? UnityEngine.Random.Range(UpMin, UpMax) : UnityEngine.Random.Range(DownMin, DownMax));
		}

		var clear = tactic.Up && ClearShot(npc, target);
		if (clear)
		{
			npc.SetDucked(false);
			brain.Events.Memory.Entity.Set(target, 0);
		}
		else
		{
			npc.SetDucked(true);
			HoldFire(brain, npc);
			Care(npc, brain, target);
		}
	}

	private static void HoldFire(BaseAIBrain brain, ScientistNPC npc)
	{
		brain.Events.Memory.Entity.Set(npc, 0);
	}

	private static bool ClearShot(ScientistNPC npc, BaseEntity target)
	{
		var from = npc.transform.position + Vector3.up * StandingEyes;
		return !Physics.Linecast(from, EyesOf(target), CoverLayers, QueryTriggerInteraction.Ignore);
	}

	private static Vector3 EyesOf(BaseEntity target)
	{
		return target is BasePlayer player && player.eyes != null ? player.eyes.position : target.transform.position + Vector3.up * 1.5f;
	}

	private static bool Safe(Vector3 standing, Spot spot, BaseEntity target)
	{
		var threat = target.transform.position;
		var toThreat = threat - standing;
		toThreat.y = 0f;
		if (toThreat.sqrMagnitude < 0.01f || Vector3.Dot(spot.Forward, toThreat.normalized) < 1f - CoverDot)
		{
			return false;
		}

		return Hidden(standing, EyesOf(target));
	}

	private static bool Hidden(Vector3 spot, Vector3 threatEyes)
	{
		return Physics.Linecast(threatEyes, spot + Vector3.up * CrouchHead, CoverLayers, QueryTriggerInteraction.Ignore)
			&& Physics.Linecast(threatEyes, spot + Vector3.up * CrouchChest, CoverLayers, QueryTriggerInteraction.Ignore);
	}

	public static void OnHurt(NpcSpawner spawner, ScientistNPC npc, BaseEntity attacker)
	{
		if (Tactics.TryGetValue(npc, out var tactic) && tactic.Mode != Mode.None)
		{
			return;
		}

		Handle(spawner, npc, attacker);
	}

	private static bool WantsCover(ScientistNPC npc, BaseAIBrain brain)
	{
		return npc.SecondsSinceAttacked < ReactWindow
			|| npc.AmmoFractionRemaining() <= 0f
			|| npc.healthFraction <= brain.HealBelowHealthFraction;
	}

	private static void Care(ScientistNPC npc, BaseAIBrain brain, BaseEntity target)
	{
		var ammo = npc.AmmoFractionRemaining();
		if (ammo < 0.25f && !npc.IsReloading())
		{
			npc.AttemptReload();
		}

		if (npc.healthFraction <= brain.HealBelowHealthFraction && UnityEngine.Random.Range(0f, 1f) <= brain.HealChance * PulseSeconds)
		{
			var healing = npc.FindHealingItem();
			if (healing != null && Vector3.Distance(npc.transform.position, target.transform.position) >= 5f)
			{
				npc.UseHealingItem(healing);
			}
		}
	}

	private static void Leave(ScientistNPC? npc, Tactic tactic)
	{
		if (npc != null && !npc.IsDestroyed)
		{
			if (tactic.Pulse != null)
			{
				npc.CancelInvoke(tactic.Pulse);
			}

			npc.SetDucked(false);
			npc.Brain?.Navigator?.ClearFacingDirectionOverride();

			if (tactic.Target != null && npc.Brain != null)
			{
				npc.Brain.Events.Memory.Entity.Set(tactic.Target, 0);
			}
		}

		if (tactic.Spot != null && tactic.Spot.User == npc)
		{
			tactic.Spot.User = null;
		}

		tactic.Spot = null;
		tactic.Mode = Mode.None;
		tactic.NextTry = Time.time + Cooldown;
	}

	private static Spot? Best(List<Spot> spots, Vector3 current, Vector3 hideFrom, Vector3 threatEyes, float minRange, float maxRange, BaseEntity forObject)
	{
		Spot? best = null;
		var bestScore = 0f;

		foreach (var spot in spots)
		{
			var position = spot.Position;
			var distance = Vector3.Distance(current, position);
			if (distance > maxRange)
			{
				continue;
			}

			if (Vector3.Distance(position, hideFrom) < Vector3.Distance(current, hideFrom) - 0.5f)
			{
				continue;
			}

			var along = position - current;
			var reach = along.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(hideFrom - current, along) / along.sqrMagnitude) : 0f;
			var nearest = current + along * reach;
			if (Vector3.Distance(nearest, hideFrom) < Mathf.Max(4f, Vector3.Distance(current, hideFrom) * 0.75f))
			{
				continue;
			}

			var flat = position - current;
			var toThreat = hideFrom - current;
			flat.y = 0f;
			toThreat.y = 0f;
			if (flat.sqrMagnitude > 1f && toThreat.sqrMagnitude > 1f && Vector3.Dot(flat.normalized, toThreat.normalized) > 0.3f)
			{
				continue;
			}

			var toHide = (hideFrom - position).normalized;
			var dot = Vector3.Dot(spot.Forward, toHide);
			if (dot < 1f - CoverDot)
			{
				continue;
			}

			if (!Hidden(position, threatEyes))
			{
				continue;
			}

			var score = 0f;
			if (spot.InUse && spot.User != forObject)
			{
				score -= 1000f;
			}

			if (minRange > 0f)
			{
				score -= (1f - Mathf.InverseLerp(0f, minRange, distance)) * 100f;
			}

			var height = Mathf.Abs(position.y - current.y);
			score += (1f - Mathf.InverseLerp(1f, 5f, height)) * 500f;
			score += Mathf.InverseLerp(1f - CoverDot, 1f, dot) * 50f;
			score += (1f - Mathf.InverseLerp(2f, maxRange, distance)) * 100f;

			var close = 1f - Mathf.InverseLerp(4f, 10f, Vector3.Distance(current, hideFrom));
			var towards = Vector3.Dot((position - current).normalized, toHide);
			score -= Mathf.InverseLerp(-1f, 0.25f, towards) * 50f * close;

			if (score > bestScore)
			{
				best = spot;
				bestScore = score;
			}
		}

		return best;
	}

	private static List<Spot> AreaFor(NpcSpawner spawner, BaseNavigator navigator)
	{
		Vector3 home = spawner.serializedNPCSpawner.position;
		var key = Vector3Int.RoundToInt(home);
		if (Areas.TryGetValue(key, out var spots))
		{
			return spots;
		}

		spots = new List<Spot>();
		WakeNavigator(navigator);
		CanWalkNear(navigator, home, 8f);

		var radius = Mathf.Clamp(spawner.RoamDistance + ExtraRadius, MinRadius, MaxRadius);
		var grill = OnGrill(home);

		AddListedCover(spots, navigator, home, radius, grill ? SandbagPrefabs : CoverPrefabs);
		var listed = spots.Count;

		if (!grill && listed < MinListedSpots)
		{
			for (var x = -radius; x <= radius && spots.Count < MaxPoints; x += Step)
			{
				for (var z = -radius; z <= radius && spots.Count < MaxPoints; z += Step)
				{
					if (x * x + z * z > radius * radius)
					{
						continue;
					}

					if (!FindGround(navigator, home + new Vector3(x, 0f, z), out var ground))
					{
						continue;
					}

					foreach (var direction in Directions)
					{
						TryAdd(spots, navigator, ground, direction);
					}
				}
			}
		}

		if (spots.Count > 0)
		{
			Areas[key] = spots;
		}

		return spots;
	}

	private static bool OnGrill(Vector3 home)
	{
		if (!Physics.Raycast(home + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, CoverLayers, QueryTriggerInteraction.Ignore) || hit.collider == null)
		{
			return false;
		}

		for (var at = hit.collider.transform; at != null; at = at.parent)
		{
			var name = Simplify(at.name);
			if (name.Contains("grill") || name.Contains("grate") || name.Contains("grating"))
			{
				return true;
			}
		}

		return false;
	}

	private static bool FindGround(BaseNavigator navigator, Vector3 sample, out Vector3 ground)
	{
		if (navigator.GetNearestNavmeshPosition(sample, out ground, 3f))
		{
			return true;
		}

		for (var lift = 2f; lift <= 6f; lift += 4f)
		{
			if (Physics.Raycast(sample + Vector3.up * lift, Vector3.down, out var floor, lift + 4f, CoverLayers, QueryTriggerInteraction.Ignore)
				&& navigator.GetNearestNavmeshPosition(floor.point, out ground, 1.5f))
			{
				return true;
			}
		}

		ground = sample;
		return false;
	}

	private static void AddListedCover(List<Spot> spots, BaseNavigator navigator, Vector3 home, float radius, string[] names)
	{
		var found = Physics.OverlapSphere(home, radius, CoverLayers, QueryTriggerInteraction.Ignore);
		Array.Sort(found, (a, b) => (a.bounds.center - home).sqrMagnitude.CompareTo((b.bounds.center - home).sqrMagnitude));
		foreach (var collider in found)
		{
			if (spots.Count >= MaxPoints)
			{
				return;
			}

			if (collider == null || !IsListedCover(collider.transform, names))
			{
				continue;
			}

			var box = collider.bounds;
			if (!navigator.GetNearestNavmeshPosition(new Vector3(box.center.x, box.min.y, box.center.z), out var floor, 4f) || box.max.y - floor.y < MinCoverHeight)
			{
				continue;
			}

			var halfX = box.extents.x + StandOff;
			var halfZ = box.extents.z + StandOff;
			var corners = new[]
			{
				new Vector3(box.center.x - halfX, floor.y, box.center.z - halfZ),
				new Vector3(box.center.x + halfX, floor.y, box.center.z - halfZ),
				new Vector3(box.center.x + halfX, floor.y, box.center.z + halfZ),
				new Vector3(box.center.x - halfX, floor.y, box.center.z + halfZ)
			};

			for (var side = 0; side < 4; side++)
			{
				var from = corners[side];
				var to = corners[(side + 1) % 4];
				var length = Vector3.Distance(from, to);
				var count = Mathf.Max(1, Mathf.FloorToInt(length / ListedSpacing));
				for (var i = 0; i <= count; i++)
				{
					AddAround(spots, navigator, Vector3.Lerp(from, to, i / (float)count), collider);
				}
			}
		}
	}

	private static void AddAround(List<Spot> spots, BaseNavigator navigator, Vector3 point, Collider cover)
	{
		if (!navigator.GetNearestNavmeshPosition(point, out var stand, 0.8f))
		{
			return;
		}

		var low = stand + Vector3.up * 0.9f;
		var toward = cover.bounds.center - low;
		toward.y = 0f;
		if (toward.sqrMagnitude < 0.01f)
		{
			return;
		}

		var facing = toward.normalized;
		if (!Physics.Raycast(low, facing, out var hit, toward.magnitude + 0.5f, CoverLayers, QueryTriggerInteraction.Ignore) || hit.distance > WallReach)
		{
			return;
		}

		foreach (var spot in spots)
		{
			if ((spot.Position - stand).sqrMagnitude < SameSpot * SameSpot && Vector3.Dot(spot.Forward, facing) > 0.7f)
			{
				return;
			}
		}

		spots.Add(new Spot { Position = stand, Forward = facing });
	}

	private static bool IsListedCover(Transform? at, string[] names)
	{
		for (var depth = 0; at != null && depth < 8; depth++, at = at.parent)
		{
			var name = Simplify(at.name);
			foreach (var listed in names)
			{
				if (name.Contains(listed))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static string Simplify(string name)
	{
		var slash = name.LastIndexOf('/');
		if (slash >= 0)
		{
			name = name.Substring(slash + 1);
		}

		if (name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
		{
			name = name.Substring(0, name.Length - 7);
		}

		var chars = new System.Text.StringBuilder(name.Length);
		foreach (var c in name)
		{
			if (char.IsLetterOrDigit(c))
			{
				chars.Append(char.ToLowerInvariant(c));
			}
		}

		return chars.ToString();
	}

	private static void TryAdd(List<Spot> spots, BaseNavigator navigator, Vector3 ground, Vector3 direction)
	{
		var low = ground + Vector3.up * 0.9f;
		if (!Physics.Raycast(low, direction, out var hit, WallReach, CoverLayers, QueryTriggerInteraction.Ignore))
		{
			return;
		}

		if (hit.collider == null || hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)
		{
			return;
		}

		var facing = -hit.normal;
		facing.y = 0f;
		if (facing.sqrMagnitude < 0.01f)
		{
			return;
		}

		facing.Normalize();
		var behind = hit.point - facing * StandOff;
		behind.y = ground.y;

		if (!navigator.GetNearestNavmeshPosition(behind, out var stand, 1f))
		{
			return;
		}

		foreach (var spot in spots)
		{
			if ((spot.Position - stand).sqrMagnitude < SameSpot * SameSpot && Vector3.Dot(spot.Forward, facing) > 0.7f)
			{
				return;
			}
		}

		spots.Add(new Spot { Position = stand, Forward = facing });
	}

	private static bool CanWalkNear(BaseNavigator navigator, Vector3 spot, float range)
	{
		if (navigator.GetNearestNavmeshPosition(spot, out _, range))
		{
			return true;
		}

		var agent = navigator.Agent;
		if (agent == null)
		{
			return false;
		}

		if (!_bindLooked)
		{
			_bindLooked = true;
			_bind = agent.GetType().GetMethod("TryBindNavmesh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		try
		{
			_bind?.Invoke(agent, null);
		}
		catch (Exception e)
		{
			HapisLog.Once("NPC navmesh rebind", e);
		}

		return navigator.GetNearestNavmeshPosition(spot, out _, range);
	}

	private static void WakeNavigator(BaseNavigator navigator)
	{
		if (navigator.Agent != null && !navigator.Agent.isOnNavMesh)
		{
			navigator.SetNavMeshEnabled(true);
		}
	}

	private static void Prune()
	{
		Gone.Clear();
		foreach (var pair in Tactics)
		{
			if (pair.Key == null || pair.Key.IsDestroyed)
			{
				Gone.Add(pair.Key!);
				if (pair.Value.Spot != null)
				{
					pair.Value.Spot.User = null;
				}
			}
		}

		foreach (var entity in Gone)
		{
			Tactics.Remove(entity);
		}
	}
}
