namespace Moose.Hapis.Extra;

/// <summary>
/// Console commands (may not register from a mod; the timer does not need them).
/// </summary>
[ConsoleSystem.Factory("hapisextra")]
public class HapisExtraCommands : ConsoleSystem
{
	[ServerVar]
	public static void spawn(ConsoleSystem.Arg arg)
	{
		if (arg.Connection != null && !arg.IsAdmin)
		{
			return;
		}
		HapisExtraMod.SpawnChinook(out var msg);
		arg.ReplyWith(msg);
	}

	[ServerVar]
	public static void despawn(ConsoleSystem.Arg arg)
	{
		if (arg.Connection != null && !arg.IsAdmin)
		{
			return;
		}
		HapisExtraMod.Despawn(HapisExtraMod.Active);
		arg.ReplyWith("Chinook despawned.");
	}
}
