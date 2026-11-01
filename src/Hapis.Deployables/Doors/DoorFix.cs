using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.Deployables.Doors;

internal static class DoorFix
{
	private const float Fallback = 1.5f;

	private static readonly Dictionary<ulong, float> LastMoved = new();
	private static readonly Dictionary<ulong, Action> Settlers = new();

	private static AccessTools.FieldRef<Door, float>? _openLength;
	private static AccessTools.FieldRef<Door, float>? _closeLength;
	private static FieldInfo? _animatorField;
	private static PropertyInfo? _initialized;
	private static MethodInfo? _rebind;
	private static FieldInfo? _needsInit;
	private static MethodInfo? _updateAnimation;
	private static bool _looked;

	public static void WakeAll()
	{
		var woke = 0;
		var stuck = 0;

		foreach (var networkable in BaseNetworkable.serverEntities)
		{
			if (networkable is not Door door || !IsMapDoor(door))
			{
				continue;
			}

			switch (Wake(door))
			{
				case true:
					woke++;
					break;
				case false:
					stuck++;
					break;
			}
		}

		if (woke > 0 || stuck > 0)
		{
			HapisLog.Info("Door Fix: Fixed " + woke + " Map Doors" + (stuck > 0 ? ", " + stuck + " Could Not Be Fixed" : ""));
		}
	}

	public static bool? Wake(Door door)
	{
		Look();
		if (_animatorField == null || _initialized == null || _rebind == null || _updateAnimation == null || door.model == null)
		{
			return null;
		}

		if (_animatorField.GetValue(door.model) is not Behaviour animator || animator == null)
		{
			return null;
		}

		if (_initialized.GetValue(animator) is true)
		{
			return null;
		}

		if (!animator.gameObject.activeInHierarchy)
		{
			return false;
		}

		animator.enabled = true;
		_rebind.Invoke(animator, null);

		if (_initialized.GetValue(animator) is not true)
		{
			return false;
		}

		_needsInit?.SetValue(door, true);
		_updateAnimation.Invoke(door, new object[] { true });
		return true;
	}

	public static void BeforeAnimate(AnimatedBuildingBlock block)
	{
		if (block is not Door door || !IsMapDoor(door))
		{
			return;
		}

		Look();
		if (_animatorField == null || _initialized == null || _rebind == null || door.model == null)
		{
			return;
		}

		if (_animatorField.GetValue(door.model) is not Behaviour animator || animator == null || _initialized.GetValue(animator) is true)
		{
			return;
		}

		if (!animator.gameObject.activeInHierarchy)
		{
			return;
		}

		animator.enabled = true;
		_rebind.Invoke(animator, null);
		_needsInit?.SetValue(door, true);
	}

	public static bool MayUse(Door door)
	{
		if (!IsMapDoor(door))
		{
			return true;
		}

		Wake(door);

		if (!LastMoved.TryGetValue(door.net.ID.Value, out var at))
		{
			return true;
		}

		return Time.realtimeSinceStartup - at >= Wait(door);
	}

	public static void Moved(Door door, bool wasOpen)
	{
		if (!IsMapDoor(door) || door.IsOpen() == wasOpen)
		{
			return;
		}

		LastMoved[door.net.ID.Value] = Time.realtimeSinceStartup;
	}

	public static void FlagsChanged(Door door, BaseEntity.Flags old, BaseEntity.Flags next)
	{
		if (!IsMapDoor(door) || ((old ^ next) & BaseEntity.Flags.Open) == 0)
		{
			return;
		}

		var id = door.net.ID.Value;
		if (!Settlers.TryGetValue(id, out var settle))
		{
			settle = () => Settle(door);
			Settlers[id] = settle;
		}

		door.CancelInvoke(settle);
		door.Invoke(settle, Wait(door));
	}

	private static void Settle(Door door)
	{
		Look();
		if (door == null || door.IsDestroyed || _updateAnimation == null)
		{
			return;
		}

		_needsInit?.SetValue(door, true);
		_updateAnimation.Invoke(door, new object[] { true });
	}

	private static bool IsMapDoor(Door door) => door != null && !door.IsDestroyed && door.net != null && door.OwnerID == 0;

	private static float Wait(Door door)
	{
		Look();
		var length = 0f;

		try
		{
			var field = door.IsOpen() ? _openLength : _closeLength;
			if (field != null)
			{
				length = field(door);
			}
		}
		catch
		{
			length = 0f;
		}

		return length > 0.05f ? Mathf.Clamp(length * 1.15f + 0.2f, 0.6f, 6f) : Fallback;
	}

	private static void Look()
	{
		if (_looked)
		{
			return;
		}

		_looked = true;
		_openLength = Length("openAnimLength");
		_closeLength = Length("closeAnimLength");
		_animatorField = AccessTools.Field(typeof(Model), "animator");

		var animatorType = _animatorField?.FieldType;
		if (animatorType != null)
		{
			_initialized = AccessTools.Property(animatorType, "isInitialized");
			_rebind = AccessTools.Method(animatorType, "Rebind", Type.EmptyTypes);
		}

		_needsInit = AccessTools.Field(typeof(AnimatedBuildingBlock), "animatorNeedsInitializing");
		_updateAnimation = AccessTools.Method(typeof(AnimatedBuildingBlock), "UpdateAnimationParameters", new[] { typeof(bool) });
	}

	private static AccessTools.FieldRef<Door, float>? Length(string name)
	{
		try
		{
			return AccessTools.Field(typeof(Door), name) != null ? AccessTools.FieldRefAccess<Door, float>(name) : null;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
