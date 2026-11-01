using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.IO.Apartments;

internal static class StandingBreakIn
{
	private const float Reach = 3f;
	private const float CheckSeconds = 0.25f;

	private static readonly Dictionary<ulong, ApartmentDoor> Active = new();

	private static FieldInfo? _starts;
	private static MethodInfo? _complete;
	private static bool _looked;

	public static bool TryStart(ApartmentDoor door, BasePlayer? player)
	{
		if (door == null || player == null || !CustomApartments.Present || !HoldingMasterKey(player))
		{
			return false;
		}

		var room = BaseNetworkable.serverEntities.Find(door.ApartmentId) as ApartmentRoom;
		if (room == null || !room.IsCurrentlyRented() || room.IsAuthed(player.userID.Get(), default) || room.IsBreakInActive())
		{
			return false;
		}

		Look();
		if (_starts == null || _complete == null || _starts.GetValue(door) is not Dictionary<ulong, TimeSince> starts)
		{
			return false;
		}

		var id = player.userID.Get();
		if (Active.ContainsKey(id))
		{
			return true;
		}

		starts[id] = 0f;
		Active[id] = door;
		player.ChatMessage("Breaking in... stay at the door and keep the master key out.");

		var started = Time.time;
		Action? tick = null;
		tick = () =>
		{
			if (door == null || door.IsDestroyed || player == null || !player.IsConnected || player.IsDead() || !HoldingMasterKey(player)
				|| Vector3.Distance(player.transform.position, door.transform.position) > Reach)
			{
				Stop(door, player, id, tick);
				player?.ChatMessage("Break in cancelled.");
				return;
			}

			if (Time.time - started < ConVar.ApartmentCommands.breakinseconds)
			{
				return;
			}

			Stop(door, player, id, tick);
			starts[id] = ConVar.ApartmentCommands.breakinseconds;

			try
			{
				_complete.Invoke(door, new object[] { player });
			}
			catch (Exception e)
			{
				HapisLog.Once("standing break in", e);
			}
		};

		door.InvokeRepeating(tick, CheckSeconds, CheckSeconds);
		return true;
	}

	private static void Stop(ApartmentDoor? door, BasePlayer? player, ulong id, Action? tick)
	{
		Active.Remove(id);
		if (door != null && tick != null)
		{
			door.CancelInvoke(tick);
		}
	}

	private static bool HoldingMasterKey(BasePlayer player)
	{
		var item = player.GetActiveItem();
		var key = ItemManager.Items != null ? ItemManager.Items.MasterKey : null;
		return item != null && key != null && item.info == key && item.amount >= 1;
	}

	private static void Look()
	{
		if (_looked)
		{
			return;
		}

		_looked = true;
		_starts = AccessTools.Field(typeof(ApartmentDoor), "breakInStarts");
		_complete = AccessTools.Method(typeof(ApartmentDoor), "CompleteBreakIn", new[] { typeof(BasePlayer) });
	}
}
