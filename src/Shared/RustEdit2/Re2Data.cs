using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using UnityEngine;

namespace Moose.Hapis.RustEdit2;

internal static class Re2Data
{
	private const int MaxOutputs = 8;
	private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

	private static HashSet<string>? _profileNames;

	public static byte[]? Xml(string marker)
	{
		try
		{
			if (!Re2Map.Usable)
			{
				return null;
			}

			return marker switch
			{
				"SerializedIOData" => Write(marker, Io),
				"SerializedNPCData" => Write(marker, Npcs),
				"SerializedPathList" => CargoPath(marker),
				"SerializedLootableContainerData" => Re2Map.LootProfiles == null ? null : Write(marker, LootProfiles),
				"SerializedVendingContainerData" => Re2Map.VendingProfiles == null ? null : Write(marker, VendingProfiles),
				_ => null
			};
		}
		catch (Exception e)
		{
			HapisLog.Once("RustEdit 2 " + marker, e);
			return null;
		}
	}

	public static List<PrefabData> Prefabs()
	{
		var list = new List<PrefabData>();
		if (!Re2Map.Usable)
		{
			return list;
		}

		foreach (var entity in Re2Map.Entities)
		{
			if (entity.Type != "Prefab" || entity.PrefabId == 0)
			{
				continue;
			}

			var category = string.IsNullOrEmpty(entity.Category) ? "Decor" : entity.Category;
			var profile = ProfileOf(entity);
			if (profile != null)
			{
				category = category.Split(':')[0] + ":::" + profile;
			}

			list.Add(new PrefabData
			{
				category = category,
				id = entity.PrefabId,
				position = new VectorData(entity.Position.x, entity.Position.y, entity.Position.z),
				rotation = new VectorData(entity.Rotation.x, entity.Rotation.y, entity.Rotation.z),
				scale = new VectorData(entity.Scale.x, entity.Scale.y, entity.Scale.z)
			});
		}

		return list;
	}

	private static byte[] Write(string root, Action<XmlWriter> body)
	{
		using var stream = new MemoryStream();
		using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
		{
			writer.WriteStartDocument();
			writer.WriteStartElement(root);
			writer.WriteAttributeString("xmlns", "xsi", null, XsiNamespace);
			body(writer);
			writer.WriteEndElement();
			writer.WriteEndDocument();
		}

		return stream.ToArray();
	}

	private static void Io(XmlWriter writer)
	{
		writer.WriteStartElement("entities");
		foreach (var entity in Re2Map.Entities)
		{
			if (entity.Type != "Prefab" || entity.Part("IOEntity") == null)
			{
				continue;
			}

			var path = entity.PrefabPath;
			if (path == null)
			{
				continue;
			}

			writer.WriteStartElement("SerializedIOEntity");
			Value(writer, "fullPath", path);
			Vector(writer, "position", entity.Position);
			writer.WriteStartElement("inputs");
			writer.WriteEndElement();
			Outputs(writer, entity, path);
			Settings(writer, entity);
			writer.WriteEndElement();
		}

		writer.WriteEndElement();
	}

	private static void Outputs(XmlWriter writer, Re2Entity entity, string path)
	{
		var raw = entity.Part("IOEntity")?["Outputs"] as JArray;
		var used = raw == null ? 0 : raw.Select((t, i) => t is JObject ? i + 1 : 0).DefaultIfEmpty(0).Max();
		var size = Math.Min(Math.Max(OutputCount(path), used), MaxOutputs);

		writer.WriteStartElement("outputs");
		for (var slot = 0; slot < size; slot++)
		{
			var wire = raw != null && slot < raw.Count ? raw[slot] as JObject : null;
			var target = wire != null ? Re2Map.Find((uint?)wire["ConnectedEntity"] ?? 0) : null;
			var targetPath = target?.PrefabPath;

			writer.WriteStartElement("SerializedConnectionData");
			if (wire == null || target == null || targetPath == null)
			{
				writer.WriteAttributeString("nil", XsiNamespace, "true");
				writer.WriteEndElement();
				continue;
			}

			Value(writer, "fullPath", targetPath);
			Vector(writer, "position", target.Position);
			Value(writer, "input", false);
			Value(writer, "connectedTo", (int?)wire["ConnectedSlot"] ?? 0);
			Value(writer, "type", (int?)wire["Type"] ?? 0);
			writer.WriteEndElement();
		}

		writer.WriteEndElement();
	}

	private static int OutputCount(string path)
	{
		try
		{
			var prefab = GameManager.server.FindPrefab(path);
			var io = prefab != null ? prefab.GetComponent<IOEntity>() : null;
			return io != null && io.outputs != null ? Math.Min(io.outputs.Length, MaxOutputs) : -1;
		}
		catch
		{
			return -1;
		}
	}

	private static void Settings(XmlWriter writer, Re2Entity entity)
	{
		var accessLevel = 0;
		var doorEffect = 0;
		var timerLength = 0f;

		var reader = entity.Part("CardReader");
		if (reader != null)
		{
			accessLevel = Int(reader, "Level", 0);
			timerLength = Float(reader, "Duration", 0f);
		}

		var timer = entity.Part("TimerSwitch");
		if (timer != null)
		{
			timerLength = Float(timer, "TimerLength", 0f);
		}

		var button = entity.Part("PressButton");
		if (button != null)
		{
			timerLength = Float(button, "PressDuration", 0f);
		}

		var door = entity.Part("DoorManipulator");
		if (door != null)
		{
			doorEffect = Int(door, "Action", 0);
		}

		Value(writer, "accessLevel", accessLevel);
		Value(writer, "doorEffect", doorEffect);
		Value(writer, "timerLength", timerLength);
		Value(writer, "frequency", Search(entity, "Frequency", 0));
		Value(writer, "unlimitedAmmo", SearchBool(entity, "UnlimitedAmmo"));
		Value(writer, "peaceKeeper", SearchBool(entity, "PeaceKeeper"));
		Value(writer, "autoTurretWeapon", SearchText(entity, "Weapon") ?? SearchText(entity, "AutoTurretWeapon"));
		Value(writer, "branchAmount", Search(entity, "BranchAmount", 0));
		Value(writer, "targetCounterNumber", Search(entity, "TargetCounterNumber", Search(entity, "TargetNumber", 0)));
		Value(writer, "rcIdentifier", SearchText(entity, "Identifier") ?? SearchText(entity, "RcIdentifier"));
		Value(writer, "counterPassthrough", SearchBool(entity, "Passthrough") || SearchBool(entity, "CounterPassthrough"));
		Value(writer, "floors", Math.Max(1, Search(entity, "Floors", 1)));
		Value(writer, "phoneName", SearchText(entity, "PhoneName"));
	}

	private static void Npcs(XmlWriter writer)
	{
		writer.WriteStartElement("npcSpawners");
		foreach (var entity in Re2Map.Entities)
		{
			if (entity.Type != "NPCSpawner")
			{
				continue;
			}

			writer.WriteStartElement("SerializedNPCSpawner");
			Value(writer, "npcType", KindOf((string?)entity.Raw["Prefab"]));
			Value(writer, "respawnMin", (int)((float?)entity.Raw["RespawnDelayMin"] ?? 1200f));
			Value(writer, "respawnMax", (int)((float?)entity.Raw["RespawnDelayMax"] ?? 1800f));
			Vector(writer, "position", entity.Position);
			Value(writer, "category", string.Empty);
			writer.WriteEndElement();
		}

		writer.WriteEndElement();
	}

	private static byte[]? CargoPath(string root)
	{
		var patrol = Re2Map.OceanPatrols.OrderByDescending(p => p.Points.Count).FirstOrDefault();
		if (patrol == null)
		{
			return null;
		}

		return Write(root, writer =>
		{
			writer.WriteStartElement("vectorData");
			foreach (var point in patrol.Points)
			{
				Vector(writer, "VectorData", point);
			}

			writer.WriteEndElement();
		});
	}

	private static void LootProfiles(XmlWriter writer)
	{
		writer.WriteStartElement("entities");
		foreach (var token in Re2Map.LootProfiles!.OfType<JObject>())
		{
			writer.WriteStartElement("LootableContainerData");
			Value(writer, "filename", Text(token, "Filename") ?? string.Empty);
			writer.WriteStartElement("items");
			foreach (var item in (Get(token, "Items") as JArray ?? new JArray()).OfType<JObject>())
			{
				writer.WriteStartElement("LootableItemData");
				Value(writer, "shortname", Text(item, "Shortname"));
				Value(writer, "minimum", Int(item, "Minimum", 1));
				Value(writer, "maximum", Int(item, "Maximum", 1));
				Value(writer, "blueprint", Bool(item, "Blueprint"));
				writer.WriteEndElement();
			}

			writer.WriteEndElement();
			Value(writer, "respawnRateMin", Int(token, "RespawnRateMin", 1));
			Value(writer, "respawnRateMax", Int(token, "RespawnRateMax", 1));
			Value(writer, "refreshRateMin", Int(token, "RefreshRateMin", 1));
			Value(writer, "refreshRateMax", Int(token, "RefreshRateMax", 1));
			Value(writer, "spawnAmountMin", Int(token, "SpawnAmountMin", 1));
			Value(writer, "spawnAmountMax", Int(token, "SpawnAmountMax", 1));
			writer.WriteEndElement();
		}

		writer.WriteEndElement();
	}

	private static void VendingProfiles(XmlWriter writer)
	{
		writer.WriteStartElement("entities");
		foreach (var token in Re2Map.VendingProfiles!.OfType<JObject>())
		{
			writer.WriteStartElement("VendingContainerData");
			Value(writer, "filename", Text(token, "Filename") ?? string.Empty);
			writer.WriteStartElement("items");
			foreach (var item in (Get(token, "Items") as JArray ?? new JArray()).OfType<JObject>())
			{
				writer.WriteStartElement("VendingItemData");
				Value(writer, "sellItemShortname", Text(item, "SellItemShortname"));
				Value(writer, "sellItemAmount", Int(item, "SellItemAmount", 1));
				Value(writer, "sellItemBlueprint", Bool(item, "SellItemBlueprint"));
				Value(writer, "currencyItemShortname", Text(item, "CurrencyItemShortname"));
				Value(writer, "currencyItemAmount", Int(item, "CurrencyItemAmount", 1));
				Value(writer, "currencyItemBlueprint", Bool(item, "CurrencyItemBlueprint"));
				Value(writer, "weight", Int(item, "Weight", 1));
				writer.WriteEndElement();
			}

			writer.WriteEndElement();
			writer.WriteEndElement();
		}

		writer.WriteEndElement();
	}

	private static void Value(XmlWriter writer, string name, string? value)
	{
		if (value != null)
		{
			writer.WriteElementString(name, value);
		}
	}

	private static void Value(XmlWriter writer, string name, int value)
	{
		writer.WriteElementString(name, value.ToString(CultureInfo.InvariantCulture));
	}

	private static void Value(XmlWriter writer, string name, float value)
	{
		writer.WriteElementString(name, value.ToString("R", CultureInfo.InvariantCulture));
	}

	private static void Value(XmlWriter writer, string name, bool value)
	{
		writer.WriteElementString(name, value ? "true" : "false");
	}

	private static void Vector(XmlWriter writer, string name, Vector3 value)
	{
		writer.WriteStartElement(name);
		Value(writer, "x", value.x);
		Value(writer, "y", value.y);
		Value(writer, "z", value.z);
		writer.WriteEndElement();
	}

	private static int KindOf(string? prefab)
	{
		var name = (prefab ?? string.Empty).ToLowerInvariant();
		if (name.Contains("peacekeeper"))
		{
			return 1;
		}

		if (name.Contains("heavy"))
		{
			return 2;
		}

		if (name.Contains("junkpile"))
		{
			return 3;
		}

		if (name.Contains("bandit"))
		{
			return 4;
		}

		if (name.Contains("murderer"))
		{
			return 5;
		}

		if (name.Contains("scarecrow"))
		{
			return 6;
		}

		return 0;
	}

	private static string? ProfileOf(Re2Entity entity)
	{
		_profileNames ??= new HashSet<string>(
			(Re2Map.LootProfiles ?? new JArray()).Concat(Re2Map.VendingProfiles ?? new JArray())
				.OfType<JObject>()
				.Select(p => Text(p, "Filename"))
				.Where(n => !string.IsNullOrEmpty(n))
				.Select(n => n!),
			StringComparer.OrdinalIgnoreCase);

		if (_profileNames.Count == 0)
		{
			return null;
		}

		foreach (var value in entity.Data.Descendants().OfType<JValue>())
		{
			if (value.Type == JTokenType.String && value.Value is string text && _profileNames.Contains(text))
			{
				return text;
			}
		}

		return null;
	}

	private static JToken? Get(JObject owner, string name)
	{
		return owner.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
	}

	private static JToken? Search(Re2Entity entity, string name)
	{
		foreach (var part in entity.Data.Properties().Select(p => p.Value).OfType<JObject>())
		{
			var found = Get(part, name);
			if (found != null && found.Type != JTokenType.Null)
			{
				return found;
			}
		}

		return null;
	}

	private static int Search(Re2Entity entity, string name, int fallback)
	{
		var token = Search(entity, name);
		return token != null && token.Type is JTokenType.Integer or JTokenType.Float ? (int)token : fallback;
	}

	private static bool SearchBool(Re2Entity entity, string name)
	{
		var token = Search(entity, name);
		return token != null && token.Type == JTokenType.Boolean && (bool)token;
	}

	private static string? SearchText(Re2Entity entity, string name)
	{
		var token = Search(entity, name);
		return token != null && token.Type == JTokenType.String ? (string?)token : null;
	}

	private static string? Text(JObject owner, string name)
	{
		var token = Get(owner, name);
		return token != null && token.Type == JTokenType.String ? (string?)token : null;
	}

	private static int Int(JObject owner, string name, int fallback)
	{
		var token = Get(owner, name);
		return token != null && token.Type is JTokenType.Integer or JTokenType.Float ? (int)token : fallback;
	}

	private static float Float(JObject owner, string name, float fallback)
	{
		var token = Get(owner, name);
		return token != null && token.Type is JTokenType.Integer or JTokenType.Float ? (float)token : fallback;
	}

	private static bool Bool(JObject owner, string name)
	{
		var token = Get(owner, name);
		return token != null && token.Type == JTokenType.Boolean && (bool)token;
	}
}
