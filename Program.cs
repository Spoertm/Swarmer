using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Swarmer.Services;
using System;
using System.Globalization;
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

		DiscordSocketClient client = new(new() { LogLevel = LogSeverity.Error, ExclusiveBulkDelete = true });
		CommandService commands = new(new() { LogLevel = LogSeverity.Warning });
		ConfigureServices(client, commands);
		IConfiguration config = _host.Services.GetService<IConfiguration>() ?? throw new ArgumentNullException($"{_host.Services.GetService<IConfiguration>()}");

		await client.LoginAsync(TokenType.Bot, config["BotToken"]);
		await client.StartAsync();
		await client.SetGameAsync("Devil Daggers");
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

	private static void ConfigureServices(DiscordSocketClient client, CommandService commands)
	{
		_host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
				services.AddSingleton(client)
					.AddSingleton(commands)
					.AddSingleton<TwitchAPI>()
					.AddSingleton<MessageHandlerService>()
					.AddSingleton<LoggingService>()
					.AddHostedService<DdStreamsPostingService>()
					.AddDbContext<DatabaseService>())
			.Build();

		_host.Services.GetService(typeof(MessageHandlerService));
		_host.Services.GetService(typeof(LoggingService));
	}

	public static void Exit()
		=> _source.Cancel();
}
