using System;
using Moose.HapisExtra;
using UnityEngine;

namespace Moose.Hapis.Extra;

/// <summary>
/// Scheduler: startup command, pump/water filling, Chinook timer.
/// </summary>
public class HapisExtraScheduler : MonoBehaviour
{
	private const float StartupCommandDelaySeconds = 10f;
	private const float EntityCheckSeconds = 5f;
	private const float EntityGiveUpSeconds = 180f;
	private const float FillStartAfterReadySeconds = 7f;
	private const float FillCheckSeconds = 10f;

	private float _nextSpawn = -1f;
	private float _startupCommandTime = -1f;
	private float _mapReadyTime = -1f;
	private float _nextEntityCheck = -1f;
	private float _entitiesReadyTime = -1f;
	private float _nextFillCheck = -1f;
	private int _lastEntityCount = -1;
	private int _stableChecks;
	private bool _startupCommandDone;
	private bool _loggedWaiting;

	private void Update()
	{
		var cfg = HapisExtraMod.Config;
		var needEntities = cfg.KeepPumpsFull || cfg.KeepWaterFull || cfg.LogLiquidPrefabs;

		// wait until the map is fully loaded
		if (BaseNetworkable.serverEntities == null || TerrainMeta.HeightMap == null || World.Size == 0)
		{
			if (!_loggedWaiting)
			{
				_loggedWaiting = true;
				HapisExtraMod.Log("Waiting for the map to finish loading...");
			}
			return;
		}

		if (_mapReadyTime < 0f)
		{
			_mapReadyTime = Time.realtimeSinceStartup;
		}

		// run the startup console command once
		if (!_startupCommandDone)
		{
			if (string.IsNullOrEmpty(cfg.StartupCommand))
			{
				_startupCommandDone = true;
			}
			else
			{
				if (_startupCommandTime < 0f)
				{
					_startupCommandTime = Time.realtimeSinceStartup + StartupCommandDelaySeconds;
					HapisExtraMod.Log("Map ready. Running startup command in " + StartupCommandDelaySeconds + " seconds.");
				}
				else if (Time.realtimeSinceStartup >= _startupCommandTime)
				{
					_startupCommandDone = true;
					RunStartupCommand(cfg.StartupCommand);
				}
			}
		}

		// wait for the entities to finish spawning (count stays the same for ~10 seconds)
		if (_entitiesReadyTime < 0f && needEntities)
		{
			if (Time.realtimeSinceStartup >= _nextEntityCheck)
			{
				_nextEntityCheck = Time.realtimeSinceStartup + EntityCheckSeconds;
				var count = EntityList.Get().Count;

				if (count > 0 && count == _lastEntityCount)
				{
					_stableChecks++;
				}
				else
				{
					_stableChecks = 0;
				}
				_lastEntityCount = count;

				var gaveUp = Time.realtimeSinceStartup - _mapReadyTime > EntityGiveUpSeconds;
				if (_stableChecks >= 2 || gaveUp)
				{
					_entitiesReadyTime = Time.realtimeSinceStartup;
					HapisExtraMod.Log("Entities ready: " + count + " entities" + (gaveUp && _stableChecks < 2 ? " (gave up waiting for the count to settle)." : "."));
				}
			}
		}

		// pump + water filling
		if (_entitiesReadyTime >= 0f && needEntities)
		{
			if (_nextFillCheck < 0f)
			{
				_nextFillCheck = Time.realtimeSinceStartup + FillStartAfterReadySeconds;
			}
			else if (Time.realtimeSinceStartup >= _nextFillCheck)
			{
				_nextFillCheck = Time.realtimeSinceStartup + FillCheckSeconds;
				if (cfg.LogLiquidPrefabs)
				{
					LiquidCensus.RunOnce();
				}
				if (cfg.KeepPumpsFull)
				{
					PumpFiller.Run();
				}
				if (cfg.KeepWaterFull)
				{
					WaterFiller.Run();
				}
			}
		}

		// Chinook timer
		if (cfg.AutoSpawnMinutes <= 0f)
		{
			return;
		}

		if (_nextSpawn < 0f)
		{
			_nextSpawn = Time.realtimeSinceStartup + cfg.FirstSpawnDelaySeconds;
			HapisExtraMod.Log("Map ready. First Chinook in " + cfg.FirstSpawnDelaySeconds + " seconds.");
			return;
		}

		if (Time.realtimeSinceStartup < _nextSpawn)
		{
			return;
		}

		if (!HapisExtraMod.IsActiveAlive())
		{
			var ok = HapisExtraMod.SpawnChinook(out var msg);
			HapisExtraMod.Log("Timer spawn: " + msg);
			if (!ok)
			{
				_nextSpawn = Time.realtimeSinceStartup + 30f;   // retry soon if it failed
				return;
			}
		}
		_nextSpawn = Time.realtimeSinceStartup + cfg.AutoSpawnMinutes * 60f;
	}

	private static void RunStartupCommand(string command)
	{
		try
		{
			var reply = ConsoleSystem.Run(ConsoleSystem.Option.Server, command, new object[0]);
			HapisExtraMod.Log("Startup command '" + command + "' ran. Reply: " + (string.IsNullOrEmpty(reply) ? "(no reply)" : reply));
		}
		catch (Exception e)
		{
			HapisExtraMod.Log("Startup command '" + command + "' failed: " + e.Message);
		}
	}
}
