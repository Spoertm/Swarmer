using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Swarmer.Models;
using Swarmer.Models.Logging;
using Swarmer.Services;
using System.Globalization;
using System.Reflection;
using TwitchLib.Api;

namespace Swarmer;

public static class Program
{
	private static readonly CancellationTokenSource _source = new();

	private static async Task Main()
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		AppDomain.CurrentDomain.ProcessExit += Exit;
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		if (builder.Environment.IsProduction())
			SetConfigFromDb(builder);

		ConfigureLogging(builder.Configuration);
		Log.Information("Starting");

		DiscordSocketClient client = new(new() { LogLevel = LogSeverity.Error });
		CommandService commands = new(new() { LogLevel = LogSeverity.Warning });
		client.Log += OnLog;
		commands.Log += OnLog;
		TwitchAPI twitchApi = new();

		WebApplication app = ConfigureServices(builder, client, commands, twitchApi).Build();
		app.UseSwagger();
		app.UseSwaggerUI();

		RegisterEndpoints(app);

		app.Services.GetRequiredService<MessageHandlerService>();

		app.UseHttpsRedirection();
		app.UseCors(policyBuilder => policyBuilder.AllowAnyOrigin());

		twitchApi.Settings.AccessToken = app.Configuration["AccessToken"];
		twitchApi.Settings.ClientId = app.Configuration["ClientId"];
		await client.LoginAsync(TokenType.Bot, app.Configuration["BotToken"]);
		await client.StartAsync();
		await client.SetGameAsync("Devil Daggers");
		await commands.AddModulesAsync(Assembly.GetEntryAssembly(), app.Services);

		try
		{
			await app.RunAsync(_source.Token);
		}
		catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
		{
			Log.Warning("Program cancellation requested");
		}
		finally
		{
			Log.Information("Exiting");
			await client.LogoutAsync();
			await client.DisposeAsync();
			_source.Dispose();
			AppDomain.CurrentDomain.ProcessExit -= Exit;
		}
	}

	private static Task OnLog(LogMessage logMessage)
	{
		LogEventLevel logLevel = logMessage.Severity switch
		{
			LogSeverity.Critical => LogEventLevel.Fatal,
			LogSeverity.Error    => LogEventLevel.Error,
			LogSeverity.Warning  => LogEventLevel.Warning,
			LogSeverity.Info     => LogEventLevel.Information,
			LogSeverity.Verbose  => LogEventLevel.Verbose,
			LogSeverity.Debug    => LogEventLevel.Debug,
			_                    => throw new ArgumentOutOfRangeException(nameof(logMessage.Severity), logMessage.Severity, null),
		};

		string message = $"Source: {logMessage.Source}\n{logMessage.Message}";
		// ReSharper disable once TemplateIsNotCompileTimeConstantProblem
		Log.Logger.Write(logLevel, logMessage.Exception, message);
		return Task.CompletedTask;
	}

	private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		=> Log.Fatal(e.ExceptionObject as Exception, "Caught unhandled exception. IsTerminating: {}", e.IsTerminating);

	private static void RegisterEndpoints(WebApplication app)
	{
		app.MapGet("/streams", (StreamProvider twitchStreams)
			=> twitchStreams.Streams);

		app.MapGet("/", async context
			=> await context.Response.WriteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Models", "Pages", "Index.html"))));
	}

	private static void ConfigureLogging(IConfiguration config) =>
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Verbose()
			.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u4}] {Message:lj}{NewLine}{Exception}")
			.WriteTo.Discord(config.GetValue<ulong>("SwarmerLoggerId"), config["SwarmerLoggerToken"])
			.CreateLogger();

	private static WebApplicationBuilder ConfigureServices(WebApplicationBuilder builder, DiscordSocketClient client, CommandService commands, TwitchAPI twitchApi)
	{
		builder.Logging.ClearProviders();
		builder.Services
			.AddEndpointsApiExplorer()
			.AddSwaggerGen()
			.AddCors()
			.AddSingleton(client)
			.AddSingleton(commands)
			.AddSingleton(twitchApi)
			.AddSingleton<MessageHandlerService>()
			.AddSingleton<StreamProvider>()
			.AddHostedService<StreamRefresherService>()
			.AddHostedService<DdStreamsPostingService>()
			.AddHostedService<KeepDynoAliveService>()
			.AddHostedService<AccessTokenNotifierService>()
			.AddDbContext<DbService>()
			.AddHttpClient();

		return builder;
	}

	private static void SetConfigFromDb(WebApplicationBuilder builder)
	{
		using DbService dbService = new();
		string jsonConfig = dbService.SwarmerConfig.AsNoTracking().First().JsonConfig;
		string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DbConfig.json");
		File.WriteAllText(configPath, jsonConfig);
		builder.Configuration.AddJsonFile(configPath);
	}

	private static void Exit(object? sender, EventArgs e) => Exit();

	public static void Exit() => _source.Cancel();
}
