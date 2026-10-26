using System;
using UnityEngine;

namespace Moose.Hapis.Extra;

/// <summary>
/// Scheduler: startup command, pump/water filling, Chinook timer.
/// </summary>
public class HapisExtraScheduler : MonoBehaviour
{
	private const float StartupCommandDelaySeconds = 10f;
	private const float FillStartAfterReadySeconds = 7f;
	private const float FillCheckSeconds = 10f;

	private static bool _serverReady;

	private float _nextSpawn = -1f;
	private float _startupCommandTime = -1f;
	private float _nextFillCheck = -1f;
	private bool _startupCommandDone;
	private bool _loggedWaiting;

	// called once the map and save have finished loading (ServerMgr.OpenConnection, or OnLoaded after a hot reload)
	public static void MarkServerReady()
	{
		if (_serverReady)
		{
			return;
		}
		_serverReady = true;
		HapisExtraMod.Log("Server ready.");
	}

	private void Update()
	{
		const bool fillsEnabled = HapisExtraConstants.KeepPumpsFull || HapisExtraConstants.KeepWaterFull;

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

		// run the startup console command once
		if (!_startupCommandDone)
		{
			if (string.IsNullOrEmpty(HapisExtraConstants.StartupCommand))
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
					RunStartupCommand(HapisExtraConstants.StartupCommand);
				}
			}
		}

		// pump + water filling, once the server has finished loading
		if (fillsEnabled && _serverReady)
		{
			if (_nextFillCheck < 0f)
			{
				_nextFillCheck = Time.realtimeSinceStartup + FillStartAfterReadySeconds;
			}
			else if (Time.realtimeSinceStartup >= _nextFillCheck)
			{
				_nextFillCheck = Time.realtimeSinceStartup + FillCheckSeconds;
				if (HapisExtraConstants.KeepPumpsFull)
				{
					PumpFiller.Run();
				}
				if (HapisExtraConstants.KeepWaterFull)
				{
					WaterFiller.Run();
				}
			}
		}

		if (_nextSpawn < 0f)
		{
			_nextSpawn = Time.realtimeSinceStartup + HapisExtraConstants.FirstSpawnDelaySeconds;
			HapisExtraMod.Log("Map ready. First Chinook in " + HapisExtraConstants.FirstSpawnDelaySeconds + " seconds.");
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
		_nextSpawn = Time.realtimeSinceStartup + HapisExtraConstants.AutoSpawnMinutes * 60f;
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
