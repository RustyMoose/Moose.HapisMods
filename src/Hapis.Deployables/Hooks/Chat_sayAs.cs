using ConVar;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(Chat), nameof(Chat.sayAs))]
internal class ChatSayAs
{
	[HarmonyPrefix]
	private static bool Prefix(Chat.ChatChannel targetChannel, ulong userId, string username, string message, BasePlayer player = null)
	{
		try
		{
			return HapisDeployables.Instance.SayAsHandler(player, message, targetChannel);
		}
		catch
		{
			return true;
		}
	}
}