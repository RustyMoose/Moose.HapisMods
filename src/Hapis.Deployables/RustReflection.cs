using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Moose.Hapis.Deployables;

internal static class RustReflection
{
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _loggedRefill;
    private static bool _loggedLimited;

    // Was: npcVendingMachine.refillTimes = array;
    public static void SetRefillTimes(NPCVendingMachine machine, float[] value)
    {
        try
        {
            // Old name first
            var field = AccessTools.Field(typeof(NPCVendingMachine), "refillTimes");

            // Fallback: any float[] field on the class or its parents
            if (field == null)
            {
                for (var t = typeof(NPCVendingMachine); t != null && field == null; t = t.BaseType)
                {
                    field = t.GetFields(AllInstance | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(f => f.FieldType == typeof(float[]));
                }
            }

            if (field != null && field.FieldType.IsAssignableFrom(typeof(float[])))
            {
                field.SetValue(machine, value);
                return;
            }

            if (!_loggedRefill)
            {
                _loggedRefill = true;
                UnityEngine.Debug.LogError("[Hapis.Deployables] NPCVendingMachine.refillTimes not found. Fields: " +
                    string.Join(", ", typeof(NPCVendingMachine).GetFields(AllInstance)
                        .Select(f => f.FieldType.Name + " " + f.Name)));
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[Hapis.Deployables] SetRefillTimes failed: " + e.Message);
        }
    }

    // Was: doorManipulator._limitedNetworking = true;
    public static void SetLimitedNetworking(BaseEntity entity, bool value)
    {
        try
        {
            // Old name first
            var field = AccessTools.Field(typeof(BaseEntity), "_limitedNetworking");
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(entity, value);
                return;
            }

            // Likely replacement: public property limitNetworking
            var prop = AccessTools.Property(typeof(BaseEntity), "limitNetworking");
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
            {
                prop.SetValue(entity, value, null);
                return;
            }

            if (!_loggedLimited)
            {
                _loggedLimited = true;
                UnityEngine.Debug.LogError("[Hapis.Deployables] _limitedNetworking / limitNetworking not found on BaseEntity. Bool members: " +
                    string.Join(", ", typeof(BaseEntity).GetFields(AllInstance)
                        .Where(f => f.FieldType == typeof(bool) && f.Name.ToLower().Contains("network"))
                        .Select(f => f.Name)));
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[Hapis.Deployables] SetLimitedNetworking failed: " + e.Message);
        }
    }
}