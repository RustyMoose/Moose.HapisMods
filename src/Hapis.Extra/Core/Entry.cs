using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.Extra;

internal static class Log
{
	public const string BrandLine = "HARMONY.HAPISEXTRAS BY BUNNYYY";

	private const string Tag = "[Hapis.Extra] ";
	private static readonly HashSet<string> Reported = new();
	private static readonly List<string> Waiting = new();
	private static bool _branded;

	public static void Brand()
	{
		if (_branded)
		{
			return;
		}

		_branded = true;
		Debug.LogWarning(BrandLine);
		foreach (var line in Waiting)
		{
			Debug.LogWarning(line);
		}

		Waiting.Clear();
	}

	public static void Info(string message)
	{
		if (_branded)
		{
			Debug.LogWarning(Tag + message);
		}
		else
		{
			Waiting.Add(Tag + message);
		}
	}

	public static void Once(string where, Exception e)
	{
		if (Reported.Add(where + "/" + e.GetType().Name))
		{
			Debug.LogWarning(Tag + "Problem In " + where + ": " + e);
		}
	}
}

[HarmonyPatch]
internal static class ServerReadyPatch
{
	private static MethodBase? _target;

	private static bool Prepare()
	{
		_target ??= AccessTools.GetDeclaredMethods(typeof(ServerMgr))
			.FirstOrDefault(m => m.Name == "OpenConnection" && !m.IsAbstract);
		return _target != null;
	}

	private static MethodBase TargetMethod() => _target!;

	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			Extras.Start();
		}
		catch (Exception e)
		{
			Log.Once("start", e);
		}
	}
}
