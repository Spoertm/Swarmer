﻿using Discord;
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
		private static IHost _host = null!;

		private static void Main(string[] args)
		{
			RunBotAsync().GetAwaiter().GetResult();
		}

		private static async Task RunBotAsync()
		{
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

			_config = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Models", "Config.json"))) ?? throw new InvalidOperationException("Error reading config file.");

			_client = new(new()
			{
				LogLevel = LogSeverity.Warning,
			});

			_commands = new(new()
			{
				LogLevel = LogSeverity.Warning,
			});

			await _client.LoginAsync(TokenType.Bot, _config!.BotToken);
			await _client.StartAsync();
			await _client.SetGameAsync("Devil Daggers");

			_client.Ready += OnReadyAsync;

			await _host.RunAsync();
			await Task.Delay(-1);
		}

		private static async Task OnReadyAsync()
		{
			_client.Ready -= OnReadyAsync;

			if (_client.GetChannel(_config.SwarmerActiveStreamsChannelId) is null)
				throw new("ActiveStreams channel is null.");

			_host = Host.CreateDefaultBuilder()
				.ConfigureServices(services =>
					services.AddSingleton(_client)
						.AddSingleton(_config)
						.AddSingleton(_commands)
						.AddSingleton<Helper>()
						.AddSingleton<EmbedHelper>()
						.AddSingleton<TwitchAPI>()
						.AddSingleton<MessageHandlerService>()
						.AddSingleton<LoggingService>()
						.AddHostedService<EmbedUpdateBackgroundService>())
				.Build();

			_host.Services.GetService(typeof(MessageHandlerService));
			_host.Services.GetService(typeof(LoggingService));

			await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _host.Services);
		}
	}
}
