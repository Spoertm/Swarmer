using Discord.Commands;

namespace Swarmer.Modules;

public class CommandModule : ModuleBase<SocketCommandContext>
{
	[Command("stopbot")]
	[RequireOwner]
	public async Task StopBot()
	{
		await ReplyAsync("Exiting...");
		Program.Exit();
	}
}
