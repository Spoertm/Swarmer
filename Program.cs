using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
	private static readonly CancellationTokenSource _source = new();

	private static async Task Main()
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		AppDomain.CurrentDomain.ProcessExit += (_, _) => Exit();

		DiscordSocketClient client = new(new() { LogLevel = LogSeverity.Error, ExclusiveBulkDelete = true });
		CommandService commands = new(new() { LogLevel = LogSeverity.Warning });
		IHost host = ConfigureServices(client, commands).Build();
		IConfiguration config = host.Services.GetService<IConfiguration>() ?? throw new ArgumentNullException($"{host.Services.GetService<IConfiguration>()}");

		await client.LoginAsync(TokenType.Bot, config["BotToken"]);
		await client.StartAsync();
		await client.SetGameAsync("Devil Daggers");
		await commands.AddModulesAsync(Assembly.GetEntryAssembly(), host.Services);

		host.Services.GetService<MessageHandlerService>();
		host.Services.GetService<LoggingService>();

		try
		{
			await host.RunAsync(_source.Token);
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

	private static IHostBuilder ConfigureServices(DiscordSocketClient client, CommandService commands)
		=> Host.CreateDefaultBuilder()
			.ConfigureAppConfiguration((_, config) => config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")))
			.ConfigureServices(services =>
				services.AddSingleton(client)
					.AddSingleton(commands)
					.AddSingleton<TwitchAPI>()
					.AddSingleton<MessageHandlerService>()
					.AddSingleton<LoggingService>()
					.AddHostedService<DdStreamsPostingService>()
					.AddDbContext<DatabaseService>())
			.ConfigureLogging(logging => logging.ClearProviders());

	public static void Exit()
		=> _source.Cancel();
}
