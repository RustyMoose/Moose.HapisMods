using ConVar;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(Chat), nameof(Chat.sayAs))]
internal class Chat_SayAs
{
	[HarmonyPrefix]
	private static bool Prefix(Chat.ChatChannel targetChannel, ulong userId, string username, string message, BasePlayer player = null)
	{
		try
		{
			return HapisIO.Instance.OnChatMessageHandler(player, message, targetChannel);
		}
		catch
		{
			return true;
		}
	}
}
