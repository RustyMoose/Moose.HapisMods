using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.Deployables.Doors;

internal static class DoorAntiHack
{
	private const float PlayerReach = 5f;
	private const float EdgeMargin = 0.6f;

	[ThreadStatic] private static bool _single;
	[ThreadStatic] private static Vector3 _singleFrom;
	[ThreadStatic] private static Vector3 _singleTo;

	private static FieldInfo? _fromField;
	private static FieldInfo? _toField;
	private static Type? _fromType;
	private static Type? _toType;
	private static PropertyInfo? _fromLength;
	private static PropertyInfo? _toLength;
	private static MethodInfo? _fromItem;
	private static MethodInfo? _toItem;
	[ThreadStatic] private static object[]? _args;
	private static bool _looked;

	public static void BeginSingle(Vector3 from, Vector3 to)
	{
		_single = true;
		_singleFrom = from;
		_singleTo = to;
	}

	public static void EndSingle() => _single = false;

	public static bool StillBlocks(Collider collider, BasePlayer? player)
	{
		if (collider.ToBaseEntity(false) is not Door door || door.OwnerID != 0 || player == null)
		{
			return true;
		}

		if (_single)
		{
			return Crosses(door, _singleFrom, _singleTo);
		}

		return AnyCross(door, player.transform.position);
	}

	private static bool AnyCross(Door door, Vector3 near)
	{
		Look();
		if (_fromField == null || _toField == null)
		{
			return false;
		}

		var from = _fromField.GetValue(null);
		var to = _toField.GetValue(null);
		if (from == null || to == null)
		{
			return false;
		}

		Bind(from.GetType(), to.GetType());
		if (_fromItem == null || _toItem == null)
		{
			return false;
		}

		var count = Math.Min(_fromLength?.GetValue(from) as int? ?? 0, _toLength?.GetValue(to) as int? ?? 0);
		var args = _args ??= new object[1];
		for (var i = 0; i < count; i++)
		{
			args[0] = i;
			if (_fromItem.Invoke(from, args) is not Vector3 a || _toItem.Invoke(to, args) is not Vector3 b)
			{
				continue;
			}

			if ((a - near).sqrMagnitude > PlayerReach * PlayerReach)
			{
				continue;
			}

			if (Crosses(door, a, b))
			{
				return true;
			}
		}

		return false;
	}

	private static void Bind(Type fromType, Type toType)
	{
		if (_fromType == fromType && _toType == toType)
		{
			return;
		}

		_fromType = fromType;
		_toType = toType;
		_fromLength = fromType.GetProperty("Length");
		_toLength = toType.GetProperty("Length");
		_fromItem = fromType.GetMethod("get_Item", new[] { typeof(int) });
		_toItem = toType.GetMethod("get_Item", new[] { typeof(int) });
	}

	private static bool Crosses(Door door, Vector3 from, Vector3 to)
	{
		var box = door.bounds;
		var local = door.transform;
		var a = local.InverseTransformPoint(from) - box.center;
		var b = local.InverseTransformPoint(to) - box.center;
		var size = box.size;

		int normal;
		if (size.x <= size.y && size.x <= size.z)
		{
			normal = 0;
		}
		else if (size.z <= size.y)
		{
			normal = 2;
		}
		else
		{
			normal = 1;
		}

		var sideA = a[normal];
		var sideB = b[normal];
		if (Mathf.Sign(sideA) == Mathf.Sign(sideB) && Mathf.Abs(sideA) > 0.05f && Mathf.Abs(sideB) > 0.05f)
		{
			return false;
		}

		var total = Mathf.Abs(sideA) + Mathf.Abs(sideB);
		var t = total > 0.0001f ? Mathf.Abs(sideA) / total : 0.5f;
		var hit = Vector3.Lerp(a, b, t);

		for (var axis = 0; axis < 3; axis++)
		{
			if (axis == normal)
			{
				continue;
			}

			if (Mathf.Abs(hit[axis]) > size[axis] * 0.5f + EdgeMargin)
			{
				return false;
			}
		}

		return true;
	}

	private static void Look()
	{
		if (_looked)
		{
			return;
		}

		_looked = true;
		_fromField = AccessTools.Field(typeof(AntiHack), "ToOverlapFrom");
		_toField = AccessTools.Field(typeof(AntiHack), "ToOverlapTo");
	}
}
