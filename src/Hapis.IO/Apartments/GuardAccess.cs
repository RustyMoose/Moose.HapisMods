using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moose.Hapis.IO.Apartments;

internal static class GuardAccess
{
	private const float GuardReach = 10f;
	private const float DoorReach = 3.5f;
	private const float TickSeconds = 0.25f;
	private const int MaxHops = 6;

	private sealed class Grant
	{
		public TimerSwitch Timer = null!;
		public Door Door = null!;
		public ulong Payer;
		public ulong Team;
		public float Until;
		public Action? Tick;
	}

	private static readonly Dictionary<TimerSwitch, Grant> Grants = new();

	public static void AfterPaid(BasePlayer? player, Vector3 guard)
	{
		if (player == null || !CustomApartments.Present)
		{
			return;
		}

		var timer = FindTimer(guard);
		if (timer == null || !timer.IsOn())
		{
			return;
		}

		var door = FindDoor(timer);
		if (door == null)
		{
			return;
		}

		if (Grants.TryGetValue(timer, out var old) && old.Tick != null)
		{
			timer.CancelInvoke(old.Tick);
		}

		var grant = new Grant
		{
			Timer = timer,
			Door = door,
			Payer = player.userID.Get(),
			Team = player.currentTeam,
			Until = Time.time + ConVar.ApartmentCommands.apartmentsecurityaccesstime
		};

		grant.Tick = () => Tick(grant);
		Grants[timer] = grant;
		timer.InvokeRepeating(grant.Tick, TickSeconds, TickSeconds);
		Tick(grant);

		player.ChatMessage("Access granted for you" + (grant.Team != 0 ? " and your team" : string.Empty) + ". Walk up to the door and it opens.");
	}

	private static void Tick(Grant grant)
	{
		if (grant.Timer == null || grant.Timer.IsDestroyed || grant.Door == null || grant.Door.IsDestroyed || Time.time >= grant.Until)
		{
			End(grant);
			return;
		}

		var near = false;
		foreach (var player in BasePlayer.activePlayerList)
		{
			if (player == null || player.IsDead() || !Allowed(grant, player))
			{
				continue;
			}

			if (Vector3.Distance(player.transform.position, grant.Door.transform.position) <= DoorReach)
			{
				near = true;
				break;
			}
		}

		if (near && !grant.Timer.IsOn())
		{
			grant.Timer.timerLength = Mathf.Max(1f, grant.Until - Time.time);
			grant.Timer.SwitchPressed();
		}
		else if (!near && grant.Timer.IsOn())
		{
			grant.Timer.EndTimer();
		}
	}

	private static bool Allowed(Grant grant, BasePlayer player)
	{
		var id = player.userID.Get();
		return id == grant.Payer || (grant.Team != 0 && player.currentTeam == grant.Team);
	}

	private static void End(Grant grant)
	{
		if (grant.Timer != null)
		{
			if (grant.Tick != null)
			{
				grant.Timer.CancelInvoke(grant.Tick);
			}

			if (!grant.Timer.IsDestroyed && grant.Timer.IsOn())
			{
				grant.Timer.EndTimer();
			}

			Grants.Remove(grant.Timer);
		}
	}

	private static TimerSwitch? FindTimer(Vector3 guard)
	{
		var found = new List<TimerSwitch>();
		Vis.Entities(guard, GuardReach, found);

		TimerSwitch? best = null;
		var bestDistance = float.MaxValue;
		foreach (var timer in found)
		{
			if (timer == null || timer.isClient)
			{
				continue;
			}

			var distance = Vector3.Distance(guard, timer.transform.position);
			if (distance < bestDistance)
			{
				best = timer;
				bestDistance = distance;
			}
		}

		return best;
	}

	private static Door? FindDoor(IOEntity start)
	{
		var visited = new HashSet<IOEntity>();
		var queue = new Queue<(IOEntity Entity, int Hops)>();
		queue.Enqueue((start, 0));

		while (queue.Count > 0)
		{
			var (entity, hops) = queue.Dequeue();
			if (entity == null || !visited.Add(entity))
			{
				continue;
			}

			if (entity is DoorManipulator manipulator && manipulator.targetDoor != null)
			{
				return manipulator.targetDoor;
			}

			if (hops >= MaxHops || entity.outputs == null)
			{
				continue;
			}

			foreach (var slot in entity.outputs)
			{
				var next = slot?.connectedTo?.Get(true);
				if (next != null)
				{
					queue.Enqueue((next, hops + 1));
				}
			}
		}

		return null;
	}
}
