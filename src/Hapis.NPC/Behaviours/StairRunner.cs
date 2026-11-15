using System.Collections;
using UnityEngine;

namespace Moose.Hapis.NPC.Behaviours;

internal sealed class StairRunner : MonoBehaviour
{
	private const float LevelGap = 1.5f;
	private const float FlightReach = 40f;
	private const float BoardDistance = 3f;
	private const float RunSpeed = 4.5f;
	private const float SnapReach = 4f;

	private bool _running;

	public static bool IsRunning(BaseEntity npc)
	{
		return npc != null && npc.TryGetComponent<StairRunner>(out var runner) && runner._running;
	}

	public static bool Try(BasePlayer npc, BaseNavigator? navigator, Vector3 goal)
	{
		if (npc == null || navigator == null)
		{
			return false;
		}

		var here = npc.transform.position;
		if (Mathf.Abs(goal.y - here.y) <= LevelGap)
		{
			return false;
		}

		OilRigStairs.Ensure();
		if (!OilRigStairs.Pick(here, goal, LevelGap, FlightReach, out var from, out var to))
		{
			return false;
		}

		if (!npc.TryGetComponent<StairRunner>(out var runner))
		{
			runner = npc.gameObject.AddComponent<StairRunner>();
		}

		if (runner._running)
		{
			return true;
		}

		var gap = from - here;
		gap.y = 0f;
		if (gap.magnitude <= BoardDistance)
		{
			runner.StartCoroutine(runner.Run(npc, navigator, from, to));
			return true;
		}

		var board = from;
		if (navigator.GetNearestNavmeshPosition(from, out var walkable, 2f))
		{
			board = walkable;
		}

		return navigator.SetDestination(board, BaseNavigator.NavigationSpeed.Fast, 0f, 0f);
	}

	private IEnumerator Run(BasePlayer npc, BaseNavigator navigator, Vector3 from, Vector3 to)
	{
		_running = true;
		navigator.Stop();
		navigator.SetNavMeshEnabled(false);

		var flat = to - from;
		flat.y = 0f;
		var facing = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : npc.transform.rotation;
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
				if (npc == null || npc.IsDestroyed || npc.IsDead())
				{
					_running = false;
					yield break;
				}

				travelled = Mathf.Min(length, travelled + RunSpeed * Time.deltaTime);
				npc.ServerPosition = Vector3.Lerp(start, end, length > 0f ? travelled / length : 1f);
				npc.ServerRotation = facing;
				npc.SendNetworkUpdate();
				yield return null;
			}
		}

		_running = false;
		if (navigator.GetNearestNavmeshPosition(npc.transform.position, out var walkable, SnapReach))
		{
			npc.ServerPosition = walkable;
		}

		HapisNPC.Instance?.SnapEntityToFloor(npc);
		navigator.SetNavMeshEnabled(true);
		navigator.Agent?.Warp(npc.transform.position);
		npc.SendNetworkUpdateImmediate();
	}
}
