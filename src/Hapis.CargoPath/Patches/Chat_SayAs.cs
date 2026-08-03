using ConVar;
using HarmonyLib;

namespace Moose.Hapis.CargoPath.Patches;

[HarmonyPatch(typeof(Chat), nameof(Chat.sayAs))]
internal class ChatSayAs
{
	[HarmonyPrefix]
	private static bool Prefix(Chat.ChatChannel targetChannel, ulong userId, string username, string message, BasePlayer player = null)
	{
		try
		{
			return HapisCargoPath.Instance.OnChatMessageHook(player, message, targetChannel);
		}
		catch
		{
			return true;
		}
	}
}
