using Discord.Commands;
using System.Threading.Tasks;

namespace Swarmer.Modules;

public class CommandModule : ModuleBase<SocketCommandContext>
{
	[Command("stopbot")]
	[RequireOwner]
	public async Task StopBot()
	{
		Program.Exit();
	}
}
