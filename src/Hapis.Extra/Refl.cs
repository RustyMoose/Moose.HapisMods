using System;
using System.Collections.Generic;
using System.Reflection;

namespace Moose.Hapis.Extra;

/// <summary>
/// Reflection helper (logs once if a method/field is missing).
/// </summary>
public static class Refl
{
	private static readonly HashSet<string> Warned = [];
	private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	private static void Warn(string key, string msg)
	{
		if (Warned.Add(key))
		{
			HapisExtraMod.Log("WARNING: " + msg);
		}
	}

	public static bool Call(object? target, string name, params object[] args)
	{
		if (target == null)
		{
			return false;
		}
		if (args == null)
		{
			args = new object[0];
		}

		for (var t = target.GetType(); t != null; t = t.BaseType)
		{
			foreach (var m in t.GetMethods(All))
			{
				if (m.Name != name)
				{
					continue;
				}
				var ps = m.GetParameters();
				if (ps.Length != args.Length)
				{
					continue;
				}

				var ok = true;
				for (var i = 0; i < ps.Length; i++)
				{
					if (args[i] != null && !ps[i].ParameterType.IsAssignableFrom(args[i].GetType()))
					{
						ok = false;
						break;
					}
				}
				if (!ok)
				{
					continue;
				}

				try
				{
					m.Invoke(target, args);
					return true;
				}
				catch (Exception e)
				{
					Warn(name + "-ex", name + " threw: " + e.InnerException);
					return false;
				}
			}
		}

		Warn(target.GetType().Name + "." + name, "method not found: " + target.GetType().Name + "." + name + " (" + args.Length + " args)");
		return false;
	}

	public static bool SetField(object? target, string name, object value)
	{
		if (target == null)
		{
			return false;
		}
		for (var t = target.GetType(); t != null; t = t.BaseType)
		{
			var f = t.GetField(name, All);
			if (f == null)
			{
				continue;
			}
			try
			{
				f.SetValue(target, Convert.ChangeType(value, f.FieldType));
				return true;
			}
			catch (Exception e)
			{
				Warn(name + "-set", "could not set " + name + ": " + e.Message);
				return false;
			}
		}
		Warn(target.GetType().Name + "." + name, "field not found: " + target.GetType().Name + "." + name);
		return false;
	}
}
