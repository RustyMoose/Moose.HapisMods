using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Moose.Hapis.RustEdit2;

internal sealed class Re2Entity
{
	public string Type = string.Empty;
	public uint UniqueId;
	public uint PrefabId;
	public string Category = string.Empty;
	public Vector3 Position;
	public Vector3 Rotation;
	public Vector3 Scale = Vector3.one;
	public JObject Data = new();
	public JObject Raw = new();

	public string? PrefabPath => PrefabId == 0 ? null : StringPool.Get(PrefabId);

	public JObject? Part(string name) => Data[name] as JObject;
}

internal sealed class Re2WorldPrefab
{
	public int Index;
	public uint PrefabId;
	public Vector3 Position;
	public JObject Data = new();
}

internal sealed class Re2OceanPatrol
{
	public readonly List<Vector3> Points = new();
	public float Spacing;
}

internal sealed class Re2ApcNetwork
{
	public readonly List<Vector3> Nodes = new();
	public readonly List<List<int>> Links = new();
	public readonly List<Vector3> Interest = new();
}

internal sealed class Re2NpcPath
{
	public uint EntityId;
	public int Index;
	public string Name = string.Empty;
	public bool Closed;
	public int AtEnd;
	public readonly List<Vector3> Points = new();
	public readonly List<(Vector3 Position, float Wait)> Stops = new();
}

internal static class Re2Map
{
	public const string BlockName = "rustedit";

	private static readonly Dictionary<string, byte[]> Blocks = new(StringComparer.OrdinalIgnoreCase);
	private static readonly List<Re2Entity> EntityList = new();
	private static readonly Dictionary<uint, Re2Entity> ById = new();
	private static readonly List<Re2WorldPrefab> WorldPrefabList = new();
	private static readonly List<Re2OceanPatrol> PatrolList = new();
	private static readonly List<Re2NpcPath> NpcPathList = new();
	private static readonly List<Re2ApcNetwork> ApcList = new();
	private static readonly List<string> UnusedParts = new();

	private static bool _loaded;

	public static bool Present { get; private set; }
	public static bool Readable { get; private set; }
	public static bool Locked { get; private set; }
	public static JArray? LootProfiles { get; private set; }
	public static JArray? VendingProfiles { get; private set; }
	public static JArray? LoadoutProfiles { get; private set; }

	public static IReadOnlyList<Re2Entity> Entities
	{
		get
		{
			Load();
			return EntityList;
		}
	}

	public static IReadOnlyList<Re2WorldPrefab> WorldPrefabs
	{
		get
		{
			Load();
			return WorldPrefabList;
		}
	}

	public static Re2NpcPath? FindNpcPath(uint entityId, int index)
	{
		Load();
		return NpcPathList.FirstOrDefault(p => p.EntityId == entityId && p.Index == index)
			?? NpcPathList.FirstOrDefault(p => p.EntityId == entityId);
	}

	public static IReadOnlyList<Re2ApcNetwork> ApcNetworks
	{
		get
		{
			Load();
			return ApcList;
		}
	}

	public static IReadOnlyList<Re2OceanPatrol> OceanPatrols
	{
		get
		{
			Load();
			return PatrolList;
		}
	}

	public static bool Usable
	{
		get
		{
			Load();
			return Present && Readable;
		}
	}

	public static Re2Entity? Find(uint id)
	{
		Load();
		return ById.TryGetValue(id, out var entity) ? entity : null;
	}

	public static void Load()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;

		byte[]? blob;
		try
		{
			blob = World.Serialization?.world?.maps?.FirstOrDefault(m => m != null && m.name == BlockName)?.data;
		}
		catch (Exception e)
		{
			HapisLog.Once("RustEdit 2 map data", e);
			return;
		}

		if (blob == null || blob.Length == 0)
		{
			HapisLog.Shared("Map type: RustEdit 1 (no RustEdit 2 data)");
			return;
		}

		Present = true;

		try
		{
			ReadBlocks(blob);
		}
		catch (Exception e)
		{
			HapisLog.Shared("RustEdit 2 map, but its data block could not be read: " + e.Message);
			return;
		}

		if (!Blocks.TryGetValue("validator", out var validator) || Encoding.UTF8.GetString(validator) != "validator")
		{
			Locked = true;
			HapisLog.Shared("RustEdit 2 map is locked with a key - the Hapis mods can only read unlocked maps. Export the map again without a key");
			return;
		}

		try
		{
			ReadEntities();
			ReadWorldPrefabs();
			ReadServerPaths();
			LootProfiles = ReadArray("lootprofiles");
			VendingProfiles = ReadArray("vendingprofiles");
			LoadoutProfiles = ReadArray("loadoutprofiles");
			Readable = true;
		}
		catch (Exception e)
		{
			HapisLog.Shared("RustEdit 2 map data could not be read: " + e.Message);
			return;
		}

		HapisLog.Shared("Map type: RustEdit 2 - " + EntityList.Count + " entities, " + EntityList.Count(e => e.Type == "NPCSpawner") + " NPC spawners, "
			+ WorldPrefabList.Count + " world prefab settings, " + PatrolList.Count + " ocean patrols, " + NpcPathList.Count + " NPC walk paths, " + ApcList.Count + " APC paths"
			+ (LootProfiles != null ? ", " + LootProfiles.Count + " loot profiles" : string.Empty)
			+ (VendingProfiles != null ? ", " + VendingProfiles.Count + " vending profiles" : string.Empty));

		ReportUnsupported();
	}

	private static void ReadBlocks(byte[] blob)
	{
		var reader = new ProtoReader(blob, 0, blob.Length);
		while (reader.Next(out var field, out var wire))
		{
			if (field == 4 && wire == 2)
			{
				var inner = reader.Sub();
				string? name = null;
				byte[]? data = null;
				while (inner.Next(out var f, out var w))
				{
					if (f == 1 && w == 2)
					{
						name = inner.Text();
					}
					else if (f == 2 && w == 2)
					{
						data = inner.Bytes();
					}
					else
					{
						inner.Skip(w);
					}
				}

				if (name != null && data != null)
				{
					Blocks[name] = data;
				}
			}
			else
			{
				reader.Skip(wire);
			}
		}
	}

	private static JArray? ReadArray(string name)
	{
		if (!Blocks.TryGetValue(name, out var data) || data.Length == 0)
		{
			return null;
		}

		var text = Encoding.UTF8.GetString(data).Trim();
		if (text.StartsWith("{"))
		{
			var obj = JObject.Parse(text);
			return obj.Properties().Select(p => p.Value).OfType<JArray>().FirstOrDefault() ?? new JArray(obj);
		}

		return JArray.Parse(text);
	}

	private static void ReadEntities()
	{
		var list = ReadArray("entities");
		if (list == null)
		{
			return;
		}

		foreach (var token in list)
		{
			Add(token as JObject);
		}
	}

	private static void Add(JObject? record)
	{
		if (record == null)
		{
			return;
		}

		var entity = new Re2Entity
		{
			Type = (string?)record["Type"] ?? string.Empty,
			UniqueId = (uint?)record["UniqueId"] ?? 0,
			PrefabId = (uint?)record["PrefabID"] ?? 0,
			Category = (string?)record["Category"] ?? string.Empty,
			Position = Vec(record["Position"], Vector3.zero),
			Rotation = Vec(record["Rotation"], Vector3.zero),
			Scale = Vec(record["Scale"], Vector3.one),
			Data = record["Data"] as JObject ?? new JObject(),
			Raw = record
		};

		if (entity.UniqueId != 0 && ById.ContainsKey(entity.UniqueId))
		{
			HapisLog.Shared("RustEdit 2: two entities share the id " + entity.UniqueId + ", the second one is skipped");
		}
		else
		{
			EntityList.Add(entity);
			if (entity.UniqueId != 0)
			{
				ById[entity.UniqueId] = entity;
			}
		}

		if (record["Children"] is JArray children)
		{
			foreach (var child in children)
			{
				Add(child as JObject);
			}
		}
	}

	private static void ReadWorldPrefabs()
	{
		var list = ReadArray("worldPrefabs");
		if (list == null)
		{
			return;
		}

		foreach (var token in list.OfType<JObject>())
		{
			WorldPrefabList.Add(new Re2WorldPrefab
			{
				Index = (int?)token["Index"] ?? -1,
				PrefabId = (uint?)token["PrefabID"] ?? 0,
				Position = Vec(token["Position"], Vector3.zero),
				Data = token["Data"] as JObject ?? new JObject()
			});
		}
	}

	private static void ReadServerPaths()
	{
		if (!Blocks.TryGetValue("ServerPaths", out var data) || data.Length == 0)
		{
			return;
		}

		var reader = new ProtoReader(data, 0, data.Length);
		while (reader.Next(out var field, out var wire))
		{
			if (field == 2 && wire == 2)
			{
				var patrol = new Re2OceanPatrol();
				var inner = reader.Sub();
				while (inner.Next(out var f, out var w))
				{
					if (f == 1 && w == 2)
					{
						patrol.Points.Add(ReadVector(inner.Sub()));
					}
					else if (f == 2 && w == 5)
					{
						patrol.Spacing = inner.Float();
					}
					else
					{
						inner.Skip(w);
					}
				}

				if (patrol.Points.Count > 1)
				{
					PatrolList.Add(patrol);
				}
			}
			else if (field == 3 && wire == 2)
			{
				var network = ReadApc(reader.Sub());
				if (network.Nodes.Count > 1)
				{
					ApcList.Add(network);
				}
			}
			else if (field == 6 && wire == 2)
			{
				var path = ReadNpcPath(reader.Sub());
				if (path.Points.Count > 1)
				{
					NpcPathList.Add(path);
				}
			}
			else if (field > 2 && wire == 2)
			{
				var label = field switch
				{
					4 => "cargo stops",
					5 => "splines",
					_ => "server path data " + field
				};

				if (!UnusedParts.Contains(label))
				{
					UnusedParts.Add(label);
				}

				reader.Skip(wire);
			}
			else
			{
				reader.Skip(wire);
			}
		}
	}

	private static Re2ApcNetwork ReadApc(ProtoReader reader)
	{
		var network = new Re2ApcNetwork();
		while (reader.Next(out var f, out var w))
		{
			if (f == 1 && w == 2)
			{
				var node = reader.Sub();
				var position = Vector3.zero;
				var links = new List<int>();
				while (node.Next(out var nf, out var nw))
				{
					if (nf == 1 && nw == 2)
					{
						position = ReadVector(node.Sub());
					}
					else if (nf == 2 && nw == 0)
					{
						links.Add((int)node.Varint());
					}
					else if (nf == 2 && nw == 2)
					{
						var packed = node.Sub();
						while (packed.More)
						{
							links.Add((int)packed.Varint());
						}
					}
					else
					{
						node.Skip(nw);
					}
				}

				network.Nodes.Add(position);
				network.Links.Add(links);
			}
			else if (f == 2 && w == 2)
			{
				network.Interest.Add(FirstVector(reader.Sub()));
			}
			else
			{
				reader.Skip(w);
			}
		}

		return network;
	}

	private static Re2NpcPath ReadNpcPath(ProtoReader reader)
	{
		var path = new Re2NpcPath();
		var nodes = new List<Vector3>();
		var knots = new List<Vector3>();

		while (reader.Next(out var f, out var w))
		{
			switch (f)
			{
				case 1 when w == 0:
					path.EntityId = (uint)reader.Varint();
					break;
				case 2 when w == 0:
					path.Index = (int)reader.Varint();
					break;
				case 3 when w == 2:
					path.Name = reader.Text();
					break;
				case 4 when w == 0:
					path.Closed = reader.Varint() != 0;
					break;
				case 5 when w == 0:
					path.AtEnd = (int)reader.Varint();
					break;
				case 8 when w == 2:
					nodes.Add(FirstVector(reader.Sub()));
					break;
				case 10 when w == 2:
					path.Stops.Add(ReadStop(reader.Sub()));
					break;
				case 11 when w == 2:
					ReadKnots(reader.Sub(), knots);
					break;
				default:
					reader.Skip(w);
					break;
			}
		}

		path.Points.AddRange(knots.Count > 1 ? knots : nodes);
		return path;
	}

	private static void ReadKnots(ProtoReader curve, List<Vector3> knots)
	{
		while (curve.Next(out var f, out var w))
		{
			if (f == 5 && w == 2)
			{
				knots.Add(FirstVector(curve.Sub()));
			}
			else
			{
				curve.Skip(w);
			}
		}
	}

	private static (Vector3, float) ReadStop(ProtoReader reader)
	{
		var position = Vector3.zero;
		var wait = 0f;
		while (reader.Next(out var f, out var w))
		{
			if (f == 1 && w == 2)
			{
				position = ReadVector(reader.Sub());
			}
			else if (f == 5 && w == 5)
			{
				wait = reader.Float();
			}
			else
			{
				reader.Skip(w);
			}
		}

		return (position, wait);
	}

	private static Vector3 FirstVector(ProtoReader reader)
	{
		var v = Vector3.zero;
		while (reader.Next(out var f, out var w))
		{
			if (f == 1 && w == 2)
			{
				v = ReadVector(reader.Sub());
			}
			else
			{
				reader.Skip(w);
			}
		}

		return v;
	}

	private static Vector3 ReadVector(ProtoReader reader)
	{
		var v = Vector3.zero;
		while (reader.Next(out var f, out var w))
		{
			if (w == 5 && f >= 1 && f <= 3)
			{
				v[f - 1] = reader.Float();
			}
			else
			{
				reader.Skip(w);
			}
		}

		return v;
	}

	private static void ReportUnsupported()
	{
		var types = EntityList.Where(e => e.Type != "Prefab" && e.Type != "NPCSpawner").GroupBy(e => e.Type).Select(g => g.Count() + " " + g.Key);
		var parts = UnusedParts.Concat(types).ToList();

		if (LoadoutProfiles is { Count: > 0 })
		{
			parts.Add(LoadoutProfiles.Count + " loadout profiles");
		}

		if (Blocks.TryGetValue("tropical", out var tropical) && tropical.Length > 0 && tropical[0] != 0)
		{
			parts.Add("tropical setting");
		}

		if (parts.Count > 0)
		{
			HapisLog.Shared("RustEdit 2: not supported yet and left out: " + string.Join(", ", parts));
		}
	}

	internal static Vector3 Vec(JToken? token, Vector3 fallback)
	{
		if (token is not JObject o)
		{
			return fallback;
		}

		return new Vector3((float?)o["x"] ?? fallback.x, (float?)o["y"] ?? fallback.y, (float?)o["z"] ?? fallback.z);
	}

	private sealed class ProtoReader
	{
		private readonly byte[] _data;
		private readonly int _end;
		private int _pos;

		public ProtoReader(byte[] data, int start, int end)
		{
			_data = data;
			_pos = start;
			_end = end;
		}

		public bool More => _pos < _end;

		public bool Next(out int field, out int wire)
		{
			field = 0;
			wire = 0;
			if (_pos >= _end)
			{
				return false;
			}

			var key = Varint();
			field = (int)(key >> 3);
			wire = (int)(key & 7);
			return true;
		}

		public ulong Varint()
		{
			ulong result = 0;
			var shift = 0;
			while (_pos < _end)
			{
				var b = _data[_pos++];
				result |= (ulong)(b & 0x7f) << shift;
				if (b < 0x80)
				{
					break;
				}

				shift += 7;
			}

			return result;
		}

		public ProtoReader Sub()
		{
			var length = (int)Varint();
			var reader = new ProtoReader(_data, _pos, _pos + length);
			_pos += length;
			return reader;
		}

		public byte[] Bytes()
		{
			var length = (int)Varint();
			var bytes = new byte[length];
			Buffer.BlockCopy(_data, _pos, bytes, 0, length);
			_pos += length;
			return bytes;
		}

		public string Text() => Encoding.UTF8.GetString(Bytes());

		public float Float()
		{
			var value = BitConverter.ToSingle(_data, _pos);
			_pos += 4;
			return value;
		}

		public void Skip(int wire)
		{
			switch (wire)
			{
				case 0:
					Varint();
					break;
				case 1:
					_pos += 8;
					break;
				case 2:
					_pos += (int)Varint();
					break;
				case 5:
					_pos += 4;
					break;
				default:
					_pos = _end;
					break;
			}
		}
	}
}
