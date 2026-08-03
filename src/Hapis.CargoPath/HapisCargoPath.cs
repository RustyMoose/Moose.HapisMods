using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using ConVar;
using Moose.Hapis.CargoPath.Models;
using UnityEngine;

namespace Moose.Hapis.CargoPath;

public class HapisCargoPath
{
	private const int CargoPathMaxViewDistance = 2000;
	
	public static HapisCargoPath Instance = null!;

	public SerializedPathList? SerializedPathList;

	private readonly List<BasePlayer> _viewingList = [];

	private Coroutine? _viewingThread;

	public List<Vector3> CargoPath = [];

	private readonly List<Vector3> _cargoSpawn = [];

	private readonly List<Vector3> _cargoStops = [];

	public bool InitializeHook()
	{
		if (Instance._cargoSpawn.Count != 0 || Instance._cargoStops.Count != 0)
		{
			Debug.LogWarning("[Hapis.CargoPath] Found " + Instance._cargoSpawn.Count + " Spawn Points And " + Instance._cargoStops.Count + " Stop Points");
		}
		
		return true;
	}

	public bool OnWorldSpawnPrefabHook(string category, Prefab prefab, Vector3 position, Quaternion rotation, Vector3 scale)
	{
		switch (prefab.ID)
		{
			case 2741054453u:
				Debug.Log("[Hapis.CargoPath] Found Cargo Spawn Point at " + position);
				_cargoSpawn.Add(position);
				return false;
			case 843218194:
				Debug.Log("[Hapis.CargoPath] Found Cargo Stop Point at " + position);
				_cargoStops.Add(position);
				return false;
			default:
				return true;
		}
	}

	public bool OnChatMessageHook(BasePlayer player, string message, Chat.ChatChannel _)
	{
		if (!player.IsAdmin)
		{
			return true;
		}

		if (!message.Equals("/showcargopath") && !message.Equals("//showcargopath"))
		{
			return true;
		}
		
		player.ChatMessage("Showing Cargo Path.");
		ShowNewCargoPath(player);
		
		return false;
	}

	private void ShowNewCargoPath(BasePlayer player)
	{
		if (!player.IsAdmin)
		{
			return;
		}
		
		if (_viewingList.Contains(player))
		{
			player.ChatMessage("Stopped Viewing Cargo Path");
			_viewingList.Remove(player);
		}
		else
		{
			player.ChatMessage("Started Viewing Cargo Path");
			_viewingList.Add(player);
		}
		if (_viewingThread == null && _viewingList.Count != 0)
		{
			_viewingThread = SingletonComponent<ServerMgr>.Instance.StartCoroutine(ShowCargoPath());
			return;
		}
		
		SingletonComponent<ServerMgr>.Instance.StopCoroutine(_viewingThread);
		_viewingThread = null;
	}

	public IEnumerator ShowCargoPath()
	{
		var lastNode = Vector3.zero;
		while (_viewingList.Count != 0)
		{
			foreach (var basePlayer in _viewingList.ToArray())
			{
				if (!basePlayer.IsConnected || basePlayer.IsSleeping() || !basePlayer.IsAdmin)
				{
					_viewingList.Remove(basePlayer);
					continue;
				}
				foreach (var item in Instance._cargoSpawn)
				{
					basePlayer.SendConsoleCommand("ddraw.box", 5f, Color.green, item, 10f);
					basePlayer.SendConsoleCommand("ddraw.text", 5f, Color.green, item, "<size=20>CargoSpawn</size>");
				}
				foreach (var cargoStop in Instance._cargoStops)
				{
					basePlayer.SendConsoleCommand("ddraw.box", 5f, Color.red, cargoStop, 10f);
					basePlayer.SendConsoleCommand("ddraw.text", 5f, Color.red, cargoStop, "<size=20>CargoStop</size>");
				}
				var num = 0;
				foreach (var item2 in CargoPath)
				{
					if (Vector3.Distance(item2, basePlayer.transform.position) < CargoPathMaxViewDistance)
					{
						basePlayer.SendConsoleCommand("ddraw.sphere", 5f, Color.white, item2, 20f);
						if (lastNode != Vector3.zero)
						{
							basePlayer.SendConsoleCommand("ddraw.line", 5f, Color.white, item2, lastNode);
						}
						basePlayer.SendConsoleCommand("ddraw.text", 5f, Color.white, item2, "<size=25>" + num + "</size>");
						lastNode = item2;
					}
					else
					{
						lastNode = Vector3.zero;
					}
					num++;
				}
				yield return CoroutineEx.waitForSeconds(5f);
			}
		}
		SingletonComponent<ServerMgr>.Instance.StopCoroutine(_viewingThread);
		_viewingThread = null;
	}

	#region Load Path Data

	public bool ReadMapData()
	{
		var mapDataFound = false;
		
		if (SerializedPathList != null)
		{
			return mapDataFound;
		}
		
		var serializedIOData = GetSerializedMapData();
		if (serializedIOData != null && serializedIOData.Length != 0)
		{
			SerializedPathList = DeserializeMapData<SerializedPathList>(serializedIOData, out mapDataFound);
		}
		
		Debug.LogWarning("[Hapis.CargoPath] DATA Found:" + mapDataFound);
		
		return mapDataFound;
	}

	private static T? DeserializeMapData<T>(byte[] bytes, out bool flag)
	{
		T? result2;
		try
		{
			using var stream = new MemoryStream(bytes);
			var result = (T)new XmlSerializer(typeof(T)).Deserialize(stream);
			flag = true;
			return result;
		}
		catch (Exception ex)
		{
			Debug.Log(ex.Message + " " + ex.StackTrace);
			flag = false;
			result2 = default;
		}
		return result2;
	}

	private static byte[]? GetSerializedMapData()
	{
		foreach (var map in World.Serialization.world.maps)
		{
			try
			{
				if (Encoding.ASCII.GetString(map.data).Contains("SerializedPathList"))
				{
					return map.data;
				}
			}
			catch
			{
				//
			}
		}
		
		return null;
	}

	#endregion
}
