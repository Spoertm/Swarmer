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
using System.Threading;
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
		public static CancellationTokenSource Source { get; } = new();

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
				ExclusiveBulkDelete = true,
			});

			_commands = new(new()
			{
				LogLevel = LogSeverity.Warning,
			});

			await _client.LoginAsync(TokenType.Bot, _config.BotToken);
			await _client.StartAsync();
			await _client.SetGameAsync("Devil Daggers");

			_client.Ready += OnReadyAsync;
			try
			{
				await Task.Delay(-1, Source.Token);
			}
			catch (TaskCanceledException ex)
			{
				await _client.StopAsync();
				Thread.Sleep(1000);
				await _client.LogoutAsync();
			}
			finally
			{
				Source.Dispose();
			}
		}

		private static async Task OnReadyAsync()
		{
			_client.Ready -= OnReadyAsync;

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
			Task.Run(async () => await _host.RunAsync(Source.Token), Source.Token);
		}
	}
}
