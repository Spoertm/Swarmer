using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Swarmer.Modules
{
	public class CommandModule : ModuleBase<SocketCommandContext>
	{
		private readonly DiscordSocketClient _client;

		public CommandModule(DiscordSocketClient client)
		{
			_client = client;
		}

		[Command("stopbot")]
		[RequireOwner]
		public async Task StopBot()
		{
			await _client.StopAsync();
			Thread.Sleep(1000);
			await _client.LogoutAsync();
			Environment.Exit(0);
		}
	}
}
