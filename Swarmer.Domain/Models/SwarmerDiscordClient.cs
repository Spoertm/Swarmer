using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace Swarmer.Domain.Models;

public class SwarmerDiscordClient
{
	private readonly IConfiguration _config;
	public DiscordSocketClient Client { get; }

	public SwarmerDiscordClient(IConfiguration config)
	{
		_config = config;
		Client = new(new() { LogLevel = LogSeverity.Error });
		Client.Log += OnLog;
	}

	public async Task InitAsync()
	{
		Log.Debug("Initiating {Client}", nameof(SwarmerDiscordClient));
		await Client.LoginAsync(TokenType.Bot, _config["BotToken"]);
		await Client.StartAsync();
		await Client.SetActivityAsync(new Game("DD Twitch streams", ActivityType.Watching));
	}

	private Task OnLog(LogMessage logMessage)
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

		Log.Logger.Write(logLevel, logMessage.Exception, "Source: {LogMsgSrc}\n{LogMsg}", logMessage.Source, logMessage.Message);
		return Task.CompletedTask;
	}
}
