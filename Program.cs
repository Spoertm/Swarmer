using Discord;
using Discord.Commands;
using Discord.WebSocket;
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

		ConfigureLogging();
		Log.Information("Starting");

		DiscordSocketClient client = new(new() { LogLevel = LogSeverity.Error });
		CommandService commands = new(new() { LogLevel = LogSeverity.Warning });
		client.Log += OnLog;
		commands.Log += OnLog;
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

		app.Services.GetRequiredService<MessageHandlerService>();

		app.UseHttpsRedirection();
		app.UseCors(policyBuilder => policyBuilder.AllowAnyOrigin());
		app.UseAuthorization();
		app.MapControllers();

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
			client.Dispose();
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
		app.MapGet("/streams", (StreamCache streamCache)
			=> streamCache.Cache);

		app.MapGet("/", async context
			=> await context.Response.WriteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Pages", "Index.html"))));
	}

	private static void ConfigureLogging() =>
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u4}] {Message:lj}{NewLine}{Exception}")
			.WriteTo.Discord(ulong.Parse(Environment.GetEnvironmentVariable("SwarmerLoggerId")!), Environment.GetEnvironmentVariable("SwarmerLoggerToken")!)
			.CreateLogger();

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
			.AddSingleton<StreamCache>()
			.AddHostedService<DdStreamsPostingService>()
			.AddHostedService<UpdateStreamsCacheService>()
			.AddDbContext<DatabaseService>();

		return builder;
	}

	private static void Exit(object? sender, EventArgs e) => Exit();

	public static void Exit() => _source.Cancel();
}
