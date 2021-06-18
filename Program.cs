using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Swarmer.Helpers;
using Swarmer.Models;
using Swarmer.Services;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TwitchLib.Api;

namespace Swarmer
{
	public static class Program
	{
		private static DiscordSocketClient _client = null!;
		private static CommandService _commands = null!;
		private static Config _config = null!;

		private static void Main(string[] args)
		{
			RunBotAsync().GetAwaiter().GetResult();
		}

		private static async Task RunBotAsync()
		{
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

			_config = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync("Models/Config.json")) ?? throw new InvalidOperationException("Error reading config file.");

			_client = new(new()
			{
				LogLevel = LogSeverity.Error,
			});

			_commands = new(new()
			{
				LogLevel = LogSeverity.Error,
			});

			await _client.LoginAsync(TokenType.Bot, _config!.BotToken);
			await _client.StartAsync();
			await _client.SetGameAsync("Devil Daggers");

			_client.Ready += OnReadyAsync;

			await Task.Delay(-1);
		}

		private static async Task OnReadyAsync()
		{
			_client.Ready -= OnReadyAsync;

			IHost host = Host.CreateDefaultBuilder()
				.ConfigureServices(services =>
					services.AddSingleton(_client)
						.AddSingleton(_config)
						.AddSingleton(_commands)
						.AddSingleton<Helper>()
						.AddSingleton<TwitchAPI>()
						.AddSingleton<MessageHandlerService>()
						.AddSingleton<LoggingService>()
						.AddHostedService<EmbedUpdateBackgroundService>())
				.Build();

			host.Services.GetService(typeof(MessageHandlerService));
			host.Services.GetService(typeof(LoggingService));

			await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), host.Services);
			await host.RunAsync();
		}
	}
}
