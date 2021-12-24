using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
	private static readonly string _ddTwitchGameId = Environment.GetEnvironmentVariable("DdTwitchGameId")!;

	private static async Task Main()
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		AppDomain.CurrentDomain.ProcessExit += Exit;

		DiscordSocketClient client = new(new() { LogLevel = LogSeverity.Error });
		CommandService commands = new(new() { LogLevel = LogSeverity.Warning });
		TwitchAPI twitchApi = new();

		WebApplication app = ConfigureServices(client, commands, twitchApi).Build();

		app.UseSwagger();
		app.UseSwaggerUI();

		RegisterEndpoints(app);

		twitchApi.Settings.AccessToken = Environment.GetEnvironmentVariable("AccessToken");
		twitchApi.Settings.ClientId = Environment.GetEnvironmentVariable("ClientId");
		await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("BotToken"));
		await client.StartAsync();
		await client.SetGameAsync("Devil Daggers");
		await commands.AddModulesAsync(Assembly.GetEntryAssembly(), app.Services);

		app.Services.GetService<MessageHandlerService>();
		app.Services.GetService<LoggingService>();

		app.UseHttpsRedirection();
		app.UseAuthorization();
		app.MapControllers();

		try
		{
			await app.RunAsync(_source.Token);
		}
		finally
		{
			await client.LogoutAsync();
			client.Dispose();
			_source.Dispose();
			AppDomain.CurrentDomain.ProcessExit -= Exit;
		}
	}

	private static void RegisterEndpoints(WebApplication app)
	{
		app.MapGet("/streams", async (TwitchAPI api)
			=> (await api.Helix.Streams.GetStreamsAsync(first: 50, gameIds: new() { _ddTwitchGameId })).Streams);

		app.MapGet("/", async context
			=> await context.Response.WriteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Pages", "Index.html"))));
	}

	private static WebApplicationBuilder ConfigureServices(DiscordSocketClient client, CommandService commands, TwitchAPI twitchApi)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();
		builder.Services.AddControllers();
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen();
		builder.Services
			.AddSingleton(client)
			.AddSingleton(commands)
			.AddSingleton(twitchApi)
			.AddSingleton<MessageHandlerService>()
			.AddSingleton<LoggingService>()
			.AddHostedService<DdStreamsPostingService>()
			.AddDbContext<DatabaseService>();

		return builder;
	}

	private static void Exit(object? sender, EventArgs e) => Exit();

	public static void Exit() => _source.Cancel();
}
