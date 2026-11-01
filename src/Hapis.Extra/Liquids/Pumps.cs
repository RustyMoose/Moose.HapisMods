using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Moose.Hapis.Extra;

internal static class Pumps
{
	private const float RescanSeconds = 30f;
	private const float StatusSeconds = 600f;

	private static readonly List<WaterCatcher> Found = new();
	private static readonly Dictionary<WaterCatcher, float> Carry = new();
	private static float _nextRescan;
	private static float _trickleFrom = -1f;
	private static float _lastTrickle;
	private static float _nextStatus;
	private static bool _startFilled;

	public static void Tick()
	{
		var s = Settings.Current;
		if (!s.KeepPumpsFull)
		{
			return;
		}

		var now = Time.realtimeSinceStartup;
		if (Found.Count == 0)
		{
			if (now < _nextRescan)
			{
				return;
			}

			_nextRescan = now + RescanSeconds;
			LiquidSources.FindPumps(Found);
			if (Found.Count == 0)
			{
				return;
			}

			Log.Info("Found " + Found.Count + " crude pump" + (Found.Count == 1 ? string.Empty : "s"));
		}

		Found.RemoveAll(p => p == null || p.IsDestroyed);

		if (!_startFilled)
		{
			_startFilled = true;
			var raised = 0;
			foreach (var pump in Found)
			{
				if (LiquidSources.Amount(pump) < s.PumpStartAmount && LiquidSources.SetAmount(pump, s.PumpStartAmount, LiquidSources.LiquidOf(pump, "crude.oil")))
				{
					raised++;
				}
			}

			_trickleFrom = now + Mathf.Max(0f, s.PumpTrickleDelayMinutes) * 60f;
			_lastTrickle = _trickleFrom;
			_nextStatus = _trickleFrom;
			Log.Info("Raised " + raised + " of " + Found.Count + " crude pumps to " + s.PumpStartAmount + ", slow refill in " + s.PumpTrickleDelayMinutes + " min");
			return;
		}

		if (now < _trickleFrom || s.PumpTrickleHours <= 0f || s.PumpMaxAmount <= s.PumpStartAmount)
		{
			return;
		}

		var perSecond = (s.PumpMaxAmount - s.PumpStartAmount) / (s.PumpTrickleHours * 3600f);
		var passed = now - _lastTrickle;
		_lastTrickle = now;

		foreach (var pump in Found)
		{
			var current = LiquidSources.Amount(pump);
			if (current >= s.PumpMaxAmount)
			{
				Carry.Remove(pump);
				continue;
			}

			Carry.TryGetValue(pump, out var owed);
			owed += perSecond * passed;
			var whole = (int)owed;
			if (whole >= 1)
			{
				owed -= whole;
				LiquidSources.SetAmount(pump, Mathf.Min(s.PumpMaxAmount, current + whole), LiquidSources.LiquidOf(pump, "crude.oil"));
			}

			Carry[pump] = owed;
		}

		if (now >= _nextStatus)
		{
			_nextStatus = now + StatusSeconds;
			Log.Info("Crude pumps now " + string.Join(", ", Found.Select(p => LiquidSources.Amount(p))) + " (max " + s.PumpMaxAmount + ")");
		}
	}
}
