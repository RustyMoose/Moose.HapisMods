using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.NPC.Behaviours;

internal static class OilRigStairs
{
	private const float MaxSlope = 44f;
	private const float Thickness = 0.1f;
	private const float Lift = 0.2f;
	private const int GroundLayers = (1 << 0) | (1 << 16) | (1 << 21);
	private const int PlayerRampLayer = 28;
	private const float EndReach = 0.3f;
	private const float WidthShare = 1f;
	private const int RampLayer = 0;
	public const float StepOff = 0.6f;
	private const float Sample = 0.25f;

	public static readonly List<(Vector3 Low, Vector3 High)> Flights = new();

	private static readonly List<Collider> Ramps = new();
	private static readonly HashSet<Collider> Known = new();
	private static bool _built;
	private static bool _shown;
	private static bool _synced;
	private static bool _picked;
	private static string? _first;

	public static void Picked(Collider collider)
	{
		if (_picked || collider == null || !Known.Contains(collider))
		{
			return;
		}

		_picked = true;
		HapisLog.Info("Oil Rigs: navmesh build picked up the stair ramps");
	}

	public static void Ensure()
	{
		if (!_built)
		{
			_built = true;
			Build();
		}
	}

	public static void Show(bool on)
	{
		Ensure();

		if (_shown == on || Ramps.Count == 0)
		{
			return;
		}

		_shown = on;
		foreach (var ramp in Ramps)
		{
			if (ramp != null)
			{
				ramp.enabled = on;
			}
		}

		if (on && !_synced)
		{
			_synced = true;
			Physics.SyncTransforms();
		}
	}

	private static void Build()
	{
		var steep = 0;
		var steepest = 0f;
		var blocked = 0;
		var found = 0;
		var copied = 0;
		var templates = new Dictionary<string, (Vector3 Low, Vector3 High)>();
		var missing = new List<Transform>();
		var kinds = new Dictionary<string, int[]>();
		foreach (var stairs in UnityEngine.Object.FindObjectsOfType<Transform>())
		{
			if (!IsStairs(stairs))
			{
				continue;
			}

			if (stairs.GetComponentInParent<Rust.Ai.Gen2.RustNavmeshModifierVolume>() != null || stairs.GetComponentInChildren<Rust.Ai.Gen2.RustNavmeshModifierVolume>(true) != null)
			{
				blocked++;
			}

			found++;
			var colliders = stairs.GetComponentsInChildren<Collider>(true);
			var measured = Measure(stairs, colliders, out var low, out var high, out var width, out var run);
			var flights = Flights.Count;
			var copiedRamp = CopyPlayerRamp(stairs);
			if (Flights.Count == flights && measured)
			{
				AddFlight(stairs, low, high);
			}

			var kind = Kind(stairs);
			if (!kinds.TryGetValue(kind, out var tally))
			{
				tally = new int[2];
				kinds[kind] = tally;
			}

			tally[0]++;
			if (Flights.Count > flights)
			{
				tally[1]++;
				if (!templates.ContainsKey(kind))
				{
					templates[kind] = (stairs.InverseTransformPoint(Flights[flights].Low), stairs.InverseTransformPoint(Flights[flights].High));
				}
			}
			else
			{
				missing.Add(stairs);
			}

			if (copiedRamp)
			{
				copied++;
				continue;
			}

			if (!measured)
			{
				continue;
			}

			var angle = AddRamp(stairs, colliders, low, high, width, run);
			if (angle > MaxSlope)
			{
				steep++;
				steepest = Mathf.Max(steepest, angle);
			}
		}

		if (found == 0)
		{
			return;
		}

		foreach (var stairs in missing)
		{
			var kind = Kind(stairs);
			if (!templates.TryGetValue(kind, out var template))
			{
				continue;
			}

			var from = stairs.TransformPoint(template.Low);
			var to = stairs.TransformPoint(template.High);
			from.y = GroundY(from, from.y) ?? from.y;
			to.y = GroundY(to, to.y) ?? to.y;
			Flights.Add((from, to));
			kinds[kind][1]++;
		}

		HapisLog.Info("Oil Rigs: navmesh ramps added on " + found + " oil rig stairs (" + copied + " using Rust's own stair ramps, " + blocked + " marked unwalkable by Rust, " + Flights.Count + " flights heavies can run)");
		if (_first != null)
		{
			HapisLog.Info("Oil Rigs: first stairs " + _first);
		}

		foreach (var pair in kinds)
		{
			HapisLog.Info("Oil Rigs: " + pair.Key + " - " + pair.Value[1] + " of " + pair.Value[0] + " can be run by heavies");
		}
		if (steep > 0)
		{
			HapisLog.Info("Oil Rigs: " + steep + " stairs are steeper than Rust's navmesh allows (steepest " + steepest.ToString("0.0") + " degrees) - NPCs may still not use them");
		}
	}

	public static bool Pick(Vector3 here, Vector3 goal, float levelGap, float reach, out Vector3 from, out Vector3 to)
	{
		from = to = Vector3.zero;
		var best = float.MaxValue;
		foreach (var flight in Flights)
		{
			for (var way = 0; way < 2; way++)
			{
				var a = way == 0 ? flight.Low : flight.High;
				var b = way == 0 ? flight.High : flight.Low;
				if (Mathf.Abs(a.y - here.y) > levelGap || Mathf.Abs(b.y - goal.y) >= Mathf.Abs(a.y - goal.y))
				{
					continue;
				}

				var away = Vector3.Distance(a, here);
				if (away > reach)
				{
					continue;
				}

				var score = away + Vector3.Distance(b, goal);
				if (score < best)
				{
					best = score;
					from = a;
					to = b;
				}
			}
		}

		return best < float.MaxValue;
	}

	public static bool Any(Vector3 here, float levelGap, float reach, out Vector3 from, out Vector3 to)
	{
		from = to = Vector3.zero;
		var seen = 0;
		foreach (var flight in Flights)
		{
			for (var way = 0; way < 2; way++)
			{
				var a = way == 0 ? flight.Low : flight.High;
				var b = way == 0 ? flight.High : flight.Low;
				if (Mathf.Abs(a.y - here.y) > levelGap || Vector3.Distance(a, here) > reach)
				{
					continue;
				}

				seen++;
				if (UnityEngine.Random.Range(0, seen) == 0)
				{
					from = a;
					to = b;
				}
			}
		}

		return seen > 0;
	}

	private static string Kind(Transform stairs)
	{
		var name = stairs.name;
		var slash = name.LastIndexOf('/');
		return slash >= 0 ? name.Substring(slash + 1) : name;
	}

	private static bool IsStairs(Transform t)
	{
		var name = t.name;
		return name.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
			&& name.IndexOf("oilrig", StringComparison.OrdinalIgnoreCase) >= 0
			&& name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool CopyPlayerRamp(Transform stairs)
	{
		var copied = false;
		foreach (var source in stairs.GetComponentsInChildren<MeshCollider>(true))
		{
			if (source == null || source.isTrigger || source.sharedMesh == null || source.gameObject.layer != PlayerRampLayer)
			{
				continue;
			}

			var go = new GameObject("Hapis.NPC.StairRamp");
			go.layer = RampLayer;
			go.transform.position = source.transform.position;
			go.transform.rotation = source.transform.rotation;
			go.transform.localScale = source.transform.lossyScale;

			var ramp = go.AddComponent<MeshCollider>();
			ramp.sharedMesh = source.sharedMesh;
			ramp.enabled = false;
			Ramps.Add(ramp);
			Known.Add(ramp);
			copied = true;
			FlightFrom(source);
		}

		if (copied && _first == null)
		{
			_first = Describe(stairs, stairs.GetComponentsInChildren<Collider>(true)) + " | copied Rust's own stair ramp";
		}

		return copied;
	}

	private static bool Measure(Transform stairs, Collider[] colliders, out Vector3 low, out Vector3 high, out float width, out float run)
	{
		low = high = Vector3.zero;
		width = run = 0f;
		var min = Vector3.positiveInfinity;
		var max = Vector3.negativeInfinity;
		var points = new List<Vector3>();

		foreach (var collider in colliders)
		{
			if (collider == null || collider.isTrigger)
			{
				continue;
			}

			var bounds = collider.bounds;
			points.Add(stairs.InverseTransformPoint(bounds.center));
			for (var i = 0; i < 8; i++)
			{
				var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
				var local = stairs.InverseTransformPoint(corner);
				min = Vector3.Min(min, local);
				max = Vector3.Max(max, local);
			}
		}

		if (points.Count < 2)
		{
			return false;
		}

		var size = max - min;
		var alongX = size.x >= size.z;
		run = alongX ? size.x : size.z;
		width = (alongX ? size.z : size.x) * WidthShare;
		if (run < 0.5f || size.y < 0.5f || run > 10f || width > 3f)
		{
			return false;
		}

		var lean = 0f;
		var middle = (min + max) * 0.5f;
		foreach (var point in points)
		{
			lean += (alongX ? point.x - middle.x : point.z - middle.z) * (point.y - middle.y);
		}

		var upward = (lean >= 0f ? 1f : -1f) * (alongX ? Vector3.right : Vector3.forward);
		var inset = Mathf.Min(0.4f, run * 0.1f);
		low = middle - upward * (run * 0.5f);
		high = middle + upward * (run * 0.5f);
		var lowStep = Surface(stairs, colliders, middle - upward * (run * 0.5f - inset), max.y) ?? min.y;
		var highStep = Surface(stairs, colliders, middle + upward * (run * 0.5f - inset), max.y) ?? max.y;
		low.y = Ground(stairs, low - upward * EndReach, lowStep) ?? min.y;
		high.y = Ground(stairs, high + upward * EndReach, highStep) ?? highStep;
		return high.y - low.y >= 0.5f;
	}

	private static void FlightFrom(Collider ramp)
	{
		var box = ramp.bounds;
		var hits = new List<Vector3>();
		for (var x = box.min.x; x <= box.max.x; x += Sample)
		{
			for (var z = box.min.z; z <= box.max.z; z += Sample)
			{
				if (ramp.Raycast(new Ray(new Vector3(x, box.max.y + 1f, z), Vector3.down), out var hit, box.size.y + 2f))
				{
					hits.Add(hit.point);
				}
			}
		}

		if (hits.Count < 4)
		{
			return;
		}

		var bottom = float.MaxValue;
		var top = float.MinValue;
		foreach (var hit in hits)
		{
			bottom = Mathf.Min(bottom, hit.y);
			top = Mathf.Max(top, hit.y);
		}

		if (top - bottom < 1.5f)
		{
			return;
		}

		var low = Average(hits, y => y <= bottom + 0.2f);
		var high = Average(hits, y => y >= top - 0.2f);
		var flat = high - low;
		flat.y = 0f;
		if (flat.magnitude < 1f || flat.magnitude > 12f)
		{
			return;
		}

		for (var k = 1; k < 10; k++)
		{
			var point = Vector3.Lerp(low, high, k / 10f);
			if (!ramp.Raycast(new Ray(point + Vector3.up * 1.5f, Vector3.down), out var hit, 3f) || Mathf.Abs(hit.point.y - point.y) > 0.6f)
			{
				return;
			}
		}

		var step = flat.normalized * StepOff;
		var from = low - step;
		var to = high + step;
		from.y = GroundY(from, low.y) ?? low.y;
		to.y = GroundY(to, high.y) ?? high.y;
		Flights.Add((from, to));
	}

	private static Vector3 Average(List<Vector3> points, Func<float, bool> keep)
	{
		var sum = Vector3.zero;
		var count = 0;
		foreach (var point in points)
		{
			if (keep(point.y))
			{
				sum += point;
				count++;
			}
		}

		return count > 0 ? sum / count : Vector3.zero;
	}

	private static float? GroundY(Vector3 spot, float near)
	{
		if (!Physics.Raycast(new Vector3(spot.x, near + 1f, spot.z), Vector3.down, out var hit, 2.5f, GroundLayers, QueryTriggerInteraction.Ignore))
		{
			return null;
		}

		return hit.point.y;
	}

	private static void AddFlight(Transform stairs, Vector3 low, Vector3 high)
	{
		var along = high - low;
		along.y = 0f;
		along = along.normalized * StepOff;
		Flights.Add((stairs.TransformPoint(low) - stairs.TransformDirection(along), stairs.TransformPoint(high) + stairs.TransformDirection(along)));
	}

	private static float AddRamp(Transform stairs, Collider[] colliders, Vector3 low, Vector3 high, float width, float run)
	{
		var slope = (high - low).normalized;
		var angle = Mathf.Atan2(high.y - low.y, run) * Mathf.Rad2Deg;
		var side = Vector3.Cross(slope, Vector3.up).normalized;
		var normal = Vector3.Cross(side, slope).normalized;

		var go = new GameObject("Hapis.NPC.StairRamp");
		go.layer = RampLayer;
		go.transform.position = stairs.TransformPoint((low + high) * 0.5f + normal * (Lift - Thickness * 0.5f));
		go.transform.rotation = stairs.rotation * Quaternion.LookRotation(slope, normal);

		var ramp = go.AddComponent<BoxCollider>();
		ramp.size = new Vector3(width, Thickness, (high - low).magnitude);
		ramp.enabled = false;
		Ramps.Add(ramp);
		Known.Add(ramp);

		if (_first == null)
		{
			_first = Describe(stairs, colliders) + " | ramp " + angle.ToString("0.0") + " deg, " + run.ToString("0.00") + " m long, " + width.ToString("0.00") + " m wide";
		}

		return angle;
	}

	private static float? Ground(Transform stairs, Vector3 local, float near)
	{
		var start = stairs.TransformPoint(new Vector3(local.x, near + 1f, local.z));
		if (!Physics.Raycast(start, Vector3.down, out var hit, 2.5f, GroundLayers, QueryTriggerInteraction.Ignore))
		{
			return null;
		}

		return stairs.InverseTransformPoint(hit.point).y;
	}

	private static string Describe(Transform stairs, Collider[] colliders)
	{
		var parts = new List<string>();
		foreach (var collider in colliders)
		{
			if (collider != null)
			{
				parts.Add(collider.GetType().Name + " layer " + collider.gameObject.layer + (collider.isTrigger ? " trigger" : "") + (collider.enabled ? "" : " off") + " " + collider.bounds.size.ToString("0.0"));
			}
		}

		return stairs.name + " at " + stairs.position.ToString("0.0") + ": " + string.Join(", ", parts);
	}

	private static float? Surface(Transform stairs, Collider[] colliders, Vector3 local, float top)
	{
		var start = stairs.TransformPoint(new Vector3(local.x, top + 1f, local.z));
		var ray = new Ray(start, Vector3.down);
		float? best = null;
		foreach (var collider in colliders)
		{
			if (collider == null || collider.isTrigger || !collider.enabled)
			{
				continue;
			}

			if (collider.Raycast(ray, out var hit, top + 20f))
			{
				var y = stairs.InverseTransformPoint(hit.point).y;
				if (best == null || y > best.Value)
				{
					best = y;
				}
			}
		}

		return best;
	}
}

[HarmonyPatch]
internal static class OilRigStairsPatch
{
	private static MethodBase? _target;
	private static bool _announced;

	private static bool Prepare()
	{
		_target ??= AccessTools.Method(typeof(Rust.Ai.Gen2.Nav.BackgroundTileBuilder), "StepCollect");
		if (_target == null && !_announced)
		{
			_announced = true;
			HapisLog.Info("Oil Rigs: navmesh collect hook missing - oil rig stairs won't get navmesh ramps");
		}

		return _target != null;
	}

	private static MethodBase TargetMethod() => _target!;

	[HarmonyPrefix]
	private static void Prefix()
	{
		try
		{
			OilRigStairs.Show(true);
		}
		catch (Exception e)
		{
			HapisLog.Once("Oil Rigs stairs", e);
		}
	}

	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			OilRigStairs.Show(false);
		}
		catch (Exception e)
		{
			HapisLog.Once("Oil Rigs stairs", e);
		}
	}
}

[HarmonyPatch]
internal static class OilRigStairsPickPatch
{
	private static MethodBase? _target;

	private static bool Prepare()
	{
		_target ??= AccessTools.Method(typeof(Rust.Ai.Gen2.Nav.BackgroundTileBuilder), "ProcessCollider");
		return _target != null;
	}

	private static MethodBase TargetMethod() => _target!;

	[HarmonyPrefix]
	private static void Prefix(Collider __1)
	{
		try
		{
			OilRigStairs.Picked(__1);
		}
		catch (Exception e)
		{
			HapisLog.Once("Oil Rigs stairs pick", e);
		}
	}
}
