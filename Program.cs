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

namespace Swarmer;

public static class Program
{

	private static IHost _host = null!;
	private static readonly CancellationTokenSource _source = new();

	private static async Task Main()
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

		Config config = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(Config.ConfigPath)) ?? throw new InvalidOperationException("Error reading config file.");
		DiscordSocketClient client = new(new() { LogLevel = LogSeverity.Error, ExclusiveBulkDelete = true });
		CommandService commands = new(new() { LogLevel = LogSeverity.Warning });
		await client.LoginAsync(TokenType.Bot, config.BotToken);
		await client.StartAsync();
		await client.SetGameAsync("Devil Daggers");

		ConfigureServices(client, config, commands);
		await commands.AddModulesAsync(Assembly.GetEntryAssembly(), _host.Services);
		try
		{
			await _host.RunAsync(_source.Token);
		}
		catch (TaskCanceledException)
		{
			await client.LogoutAsync();
			await client.StopAsync();
		}
		finally
		{
			_source.Dispose();
		}
	}

	private static void ConfigureServices(DiscordSocketClient client, Config config, CommandService commands)
	{
		_host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
				services.AddSingleton(client)
					.AddSingleton(config)
					.AddSingleton(commands)
					.AddSingleton<DiscordHelper>()
					.AddSingleton<TwitchAPI>()
					.AddSingleton<MessageHandlerService>()
					.AddSingleton<LoggingService>()
					.AddHostedService<DdStreamsPostingService>())
			.Build();

		_host.Services.GetService(typeof(MessageHandlerService));
		_host.Services.GetService(typeof(LoggingService));
	}

	public static void Exit()
		=> _source.Cancel();
}
