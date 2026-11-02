using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.Extra;

internal static class Extras
{
	private static bool _started;

	public static void Start()
	{
		if (_started)
		{
			return;
		}

		_started = true;
		Log.Brand();
		Settings.Load();

		var s = Settings.Current;
		foreach (var drop in s.Drops)
		{
			Log.Info("Chinook drop '" + drop.Name + "' at " + drop.X + ", " + drop.Y + ", " + drop.Z
				+ (drop.EveryHours > 0f ? " - first after " + drop.FirstHours + " h, then every " + drop.EveryHours + " h" : " - timer off"));
		}

		Log.Info("Startup command: " + (string.IsNullOrEmpty(s.StartupCommand) ? "none" : s.StartupCommand));
		Log.Info("Crude pumps: " + (s.KeepPumpsFull
			? "start at " + s.PumpStartAmount + ", after " + s.PumpTrickleDelayMinutes + " min refill to " + s.PumpMaxAmount + " over " + s.PumpTrickleHours + " h"
			: "off") + ", taking oil blocked: " + (s.BlockPumpTaking ? "yes" : "no"));
		Log.Info("Water sources: " + (s.KeepWaterFull
			? "full on start" + (s.WaterRefillHours > 0f ? ", then every " + s.WaterRefillHours + " h" : string.Empty)
			: "off"));

		var ticker = new GameObject("HapisExtras.Ticker");
		UnityEngine.Object.DontDestroyOnLoad(ticker);
		ticker.AddComponent<Ticker>();
	}

	public static void RunCommand(string command)
	{
		try
		{
			var reply = ConsoleSystem.Run(ConsoleSystem.Option.Server, command, Array.Empty<object>());
			Log.Info("Ran '" + command + "'" + (string.IsNullOrEmpty(reply) ? string.Empty : ": " + reply));
		}
		catch (Exception e)
		{
			Log.Info("Startup command '" + command + "' failed: " + e.Message);
		}
	}
}

internal class Ticker : MonoBehaviour
{
	private const float StartupCommandDelay = 10f;
	private const float FillDelay = 7f;
	private const float FillEvery = 10f;
	private const float RetrySpawn = 30f;

	private float _born;
	private float _nextBeat;
	private float _nextFill;
	private bool _commandDone;

	private void Awake()
	{
		_born = Time.realtimeSinceStartup;
		_nextFill = _born + FillDelay;
		Chinook.Setup(_born);
	}

	private void Update()
	{
		var now = Time.realtimeSinceStartup;
		if (now < _nextBeat)
		{
			return;
		}

		_nextBeat = now + 1f;
		var s = Settings.Current;

		if (!_commandDone && now >= _born + StartupCommandDelay)
		{
			_commandDone = true;
			if (!string.IsNullOrEmpty(s.StartupCommand))
			{
				Extras.RunCommand(s.StartupCommand);
			}
		}

		if (now >= _nextFill)
		{
			_nextFill = now + FillEvery;
			Safe(Pumps.Tick, "pumps");
			Safe(Water.Tick, "water");
		}

		foreach (var run in Chinook.Runs)
		{
			if (run.Spot.EveryHours <= 0f || now < run.NextAt)
			{
				continue;
			}

			var every = run.Spot.EveryHours * 3600f;
			if (run.Flying)
			{
				run.NextAt = now + every;
				continue;
			}

			var spawned = Chinook.Spawn(run, out var message);
			Log.Info("Timed Chinook: " + message);
			run.NextAt = now + (spawned ? every : RetrySpawn);
		}
	}

	private static void Safe(Action action, string where)
	{
		try
		{
			action();
		}
		catch (Exception e)
		{
			Log.Once(where, e);
		}
	}
}

[HarmonyPatch]
internal static class ChatCommandPatch
{
	private static MethodBase? _target;

	private static bool Prepare()
	{
		var shape = new[] { typeof(ConVar.Chat.ChatChannel), typeof(ulong), typeof(string), typeof(string), typeof(BasePlayer) };
		_target ??= AccessTools.GetDeclaredMethods(typeof(ConVar.Chat))
			.Where(m => m.IsStatic && !m.ContainsGenericParameters && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(shape))
			.OrderByDescending(m => string.Equals(m.Name, "sayAs", StringComparison.OrdinalIgnoreCase))
			.FirstOrDefault();
		return _target != null;
	}

	private static MethodBase TargetMethod() => _target!;

	[HarmonyPrefix]
	private static bool Prefix(string __3, BasePlayer __4)
	{
		try
		{
			if (__3 == null || __3.Length < 8 || __3[0] != '/' || __4 == null || !__4.IsAdmin)
			{
				return true;
			}

			if (string.Equals(__3, "/chinookgone", StringComparison.OrdinalIgnoreCase))
			{
				Chinook.DespawnAll();
				__4.ChatMessage("[Hapis.Extra] All Chinooks removed.");
				return false;
			}

			if (!__3.StartsWith("/chinook", StringComparison.OrdinalIgnoreCase) || (__3.Length > 8 && __3[8] != ' '))
			{
				return true;
			}

			var name = __3.Substring(8).Trim();
			var run = Chinook.Find(name);
			if (run == null)
			{
				__4.ChatMessage("[Hapis.Extra] No drop called '" + name + "'. Drops: " + string.Join(", ", Chinook.Runs.Select(r => r.Spot.Name)));
				return false;
			}

			Chinook.Spawn(run, out var message);
			__4.ChatMessage("[Hapis.Extra] " + message);
			return false;
		}
		catch (Exception e)
		{
			Log.Once("chat", e);
			return true;
		}
	}
}
