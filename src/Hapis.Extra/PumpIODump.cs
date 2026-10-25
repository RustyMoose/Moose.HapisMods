using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Moose.Hapis.Extra;

/// <summary>
/// Logs the pump's IO slots (inputs/outputs) and hose/fluid members.
/// Diagnostics only (LogPumpIO).
/// </summary>
public static class PumpIODump
{
	private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	private static int SlotCount(object pump, string name)
	{
		var a = PumpFiller.GetField(pump, name) as Array;
		return a == null ? -1 : a.Length;
	}

	public static void Run(List<BaseNetworkable> pumps)
	{
		try
		{
			if (pumps.Count == 0)
			{
				return;
			}

			var first = pumps[0];
			HapisExtraMod.Log("PumpIO: sample pump at " + first.transform.position + "  class=" + first.GetType().Name);
			DumpSlots(first, "inputs");
			DumpSlots(first, "outputs");
			DumpNamed(first);

			for (var i = 0; i < pumps.Count; i++)
			{
				var p = pumps[i];
				HapisExtraMod.Log("PumpIO: pump " + i + " at " + p.transform.position + ": inputs=" + SlotCount(p, "inputs") + " outputs=" + SlotCount(p, "outputs") + "  (-1 means the field was not found)");
			}

			HapisExtraMod.Log("PumpIO: done.");
		}
		catch (Exception e)
		{
			HapisExtraMod.Log("PumpIO failed: " + e.Message);
		}
	}

	private static void DumpSlots(object pump, string fieldName)
	{
		var slots = PumpFiller.GetField(pump, fieldName) as Array;
		if (slots == null)
		{
			HapisExtraMod.Log("PumpIO:   no '" + fieldName + "' array found on the pump.");
			return;
		}

		HapisExtraMod.Log("PumpIO:   " + fieldName + ": " + slots.Length + " slot(s)");
		for (var i = 0; i < slots.Length; i++)
		{
			var slot = slots.GetValue(i);
			if (slot == null)
			{
				HapisExtraMod.Log("PumpIO:     [" + i + "] null");
				continue;
			}

			var fields = slot.GetType().GetFields(Flags | BindingFlags.FlattenHierarchy);
			var shown = 0;
			foreach (var field in fields)
			{
				if (shown >= 25)
				{
					break;
				}
				var value = "?";
				try
				{
					var v = field.GetValue(slot);
					value = v switch
					{
						null => "null",
						Array array => "Array(" + array.Length + ")",
						_ => field.Name == "connectedTo" ? DescribeConnection(v) : v.ToString()
					};
				}
				catch (Exception)
				{
				}
				HapisExtraMod.Log("PumpIO:     [" + i + "] " + field.Name + " = " + value);
				shown++;
			}
		}
	}

	private static string DescribeConnection(object ioRef)
	{
		try
		{
			var get = ioRef.GetType().GetMethod("Get", new[] { typeof(bool) });
			if (get == null)
			{
				return ioRef.ToString() + " (no Get method)";
			}
			var ent = get.Invoke(ioRef, new object[] { true });
			if (ent == null)
			{
				return "not connected";
			}
			var comp = ent as Component;
			return ent.GetType().Name + (comp != null ? " at " + comp.transform.position : "");
		}
		catch (Exception e)
		{
			return "unreadable (" + e.Message + ")";
		}
	}

	private static void DumpNamed(object pump)
	{
		var words = new[] { "hose", "fluid", "liquid", "pipe" };
		var logged = 0;
		for (var t = pump.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
		{
			var fields = t.GetFields(Flags);
			for (var i = 0; i < fields.Length; i++)
			{
				if (logged >= 40)
				{
					return;
				}
				var lower = fields[i].Name.ToLowerInvariant();
				var match = false;
				for (var w = 0; w < words.Length; w++)
				{
					if (lower.Contains(words[w]))
					{
						match = true;
					}
				}
				if (!match)
				{
					continue;
				}

				var value = "?";
				try
				{
					var v = fields[i].GetValue(pump);
					value = v == null ? "null" : (v is Array array ? "Array(" + array.Length + ")" : v.ToString());
				}
				catch (Exception)
				{
				}
				HapisExtraMod.Log("PumpIO:   field " + t.Name + "." + fields[i].Name + " (" + fields[i].FieldType.Name + ") = " + value);
				logged++;
			}

			var methods = t.GetMethods(Flags);
			for (var i = 0; i < methods.Length; i++)
			{
				if (logged >= 40)
				{
					return;
				}
				var lower = methods[i].Name.ToLowerInvariant();
				var match = false;
				for (var w = 0; w < words.Length; w++)
				{
					if (lower.Contains(words[w]))
					{
						match = true;
					}
				}
				if (!match)
				{
					continue;
				}
				HapisExtraMod.Log("PumpIO:   method " + t.Name + "." + methods[i].Name + " (" + methods[i].GetParameters().Length + " params)");
				logged++;
			}
		}
	}
}
