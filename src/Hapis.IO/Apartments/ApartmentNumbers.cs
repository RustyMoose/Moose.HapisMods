using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Moose.Hapis.IO.Apartments;

internal static class ApartmentNumbers
{
	private const string Prefix = "B";

	private static bool _numbered;

	public static void Ensure(ApartmentRoom room)
	{
		if (room == null || !string.IsNullOrEmpty(room.RoomNumber))
		{
			return;
		}

		if (!_numbered)
		{
			_numbered = true;
			NumberAll();
		}

		if (string.IsNullOrEmpty(room.RoomNumber))
		{
			room.RoomNumber = NextFree(Taken());
		}
	}

	private static void NumberAll()
	{
		_ = CustomApartments.Present;

		var rooms = BaseNetworkable.serverEntities.OfType<ApartmentRoom>().Where(r => r != null).ToList();
		var taken = Taken();

		var unnumbered = rooms
			.Where(r => string.IsNullOrEmpty(r.RoomNumber))
			.OrderBy(r => Mathf.Round(r.transform.position.y))
			.ThenBy(r => Mathf.Round(r.transform.position.x * 10f))
			.ThenBy(r => Mathf.Round(r.transform.position.z * 10f))
			.ToList();

		foreach (var room in unnumbered)
		{
			room.RoomNumber = NextFree(taken);
			taken.Add(room.RoomNumber);
		}

		if (unnumbered.Count > 0)
		{
			HapisLog.Info("Apartments: Numbered " + unnumbered.Count + " Custom Apartments");
		}
	}

	public static void LinkMailboxes(ApartmentBuilding building)
	{
		if (!_numbered)
		{
			_numbered = true;
			NumberAll();
		}

		var rooms = BaseNetworkable.serverEntities.OfType<ApartmentRoom>()
			.Where(r => r != null && !string.IsNullOrEmpty(r.RoomNumber) && r.RoomNumber.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
			.ToList();
		var names = new HashSet<string>(BaseNetworkable.serverEntities.OfType<ApartmentRoom>()
			.Where(r => r != null && !string.IsNullOrEmpty(r.RoomNumber))
			.Select(r => r.RoomNumber), StringComparer.OrdinalIgnoreCase);

		var mailboxes = BaseNetworkable.serverEntities.OfType<ApartmentMailbox>().Where(m => m != null).ToList();
		var used = new HashSet<string>(mailboxes
			.Where(m => !string.IsNullOrEmpty(m.RoomNumber) && names.Contains(m.RoomNumber))
			.Select(m => m.RoomNumber), StringComparer.OrdinalIgnoreCase);

		var free = rooms
			.Where(r => !used.Contains(r.RoomNumber))
			.OrderBy(r => Order(r.RoomNumber))
			.ToList();

		var loose = mailboxes
			.Where(m => string.IsNullOrEmpty(m.RoomNumber) || !names.Contains(m.RoomNumber))
			.OrderBy(m => Mathf.Round(m.transform.position.y))
			.ThenBy(m => Mathf.Round(m.transform.position.x * 10f))
			.ThenBy(m => Mathf.Round(m.transform.position.z * 10f))
			.ThenBy(m => m.ShortPrefabName, StringComparer.OrdinalIgnoreCase)
			.ThenBy(m => m.net != null ? m.net.ID.Value : 0uL)
			.ToList();

		var linked = 0;
		for (var i = 0; i < loose.Count && i < free.Count; i++)
		{
			loose[i].RoomNumber = free[i].RoomNumber;
			linked++;
		}

		foreach (var mailbox in mailboxes)
		{
			if (string.IsNullOrEmpty(mailbox.RoomNumber) || !names.Contains(mailbox.RoomNumber) || mailbox.Room != null)
			{
				continue;
			}

			mailbox.Room = building != null ? building.FindByRoomNumber(mailbox.RoomNumber) : null;
			if (mailbox.Room == null)
			{
				mailbox.Room = rooms.FirstOrDefault(r => string.Equals(r.RoomNumber, mailbox.RoomNumber, StringComparison.OrdinalIgnoreCase));
			}

			mailbox.SendNetworkUpdate();
		}

		if (loose.Count > 0)
		{
			HapisLog.Info("Apartments: Linked " + linked + " Custom Mailboxes" + (loose.Count > linked ? ", " + (loose.Count - linked) + " Spare With No Room" : ""));
		}
	}

	private static int Order(string number)
	{
		return int.TryParse(number.Substring(Prefix.Length), out var value) ? value : int.MaxValue;
	}

	private static HashSet<string> Taken()
	{
		return new HashSet<string>(BaseNetworkable.serverEntities.OfType<ApartmentRoom>()
			.Where(r => r != null && !string.IsNullOrEmpty(r.RoomNumber))
			.Select(r => r.RoomNumber), StringComparer.OrdinalIgnoreCase);
	}

	private static string NextFree(HashSet<string> taken)
	{
		var number = 1;
		while (taken.Contains(Prefix + number))
		{
			number++;
		}

		return Prefix + number;
	}
}
