using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using Serilog.Events;
using Swarmer.Domain.Models;

namespace Swarmer.Domain.Discord;

public class SwarmerDiscordClient : DiscordSocketClient
{
	private readonly SwarmerConfig _config;

	public SwarmerDiscordClient(IOptions<SwarmerConfig> options, DiscordSocketConfig socketConfig)
		: base(socketConfig)
	{
		_config = options.Value;

		Log += OnLog;
		Ready += () =>
		{
			MessageReceived += message => Task.Run(() => ClientOnMessageReceived(message));
			return Task.CompletedTask;
		};
	}

	private async Task ClientOnMessageReceived(SocketMessage msg)
	{
		if (msg is not SocketUserMessage { Source: MessageSource.User } message)
		{
			return;
		}

		bool messageMentionsBot = message.Content.StartsWith($"<@{CurrentUser.Id}>") || message.Content.StartsWith($"<!{CurrentUser.Id}>");
		if (messageMentionsBot && Emote.TryParse("<a:swarmer:855162753093337109>", out Emote swarmerEmote))
		{
			await msg.AddReactionAsync(swarmerEmote);
		}
	}

	public async Task InitAsync()
	{
		Serilog.Log.Debug("Initiating {Client}", nameof(SwarmerDiscordClient));
		await LoginAsync(TokenType.Bot, _config.BotToken);
		await StartAsync();
		await SetActivityAsync(new Game("DD Twitch streams", ActivityType.Watching));
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

		Serilog.Log.Logger.Write(logLevel, logMessage.Exception, "Source: {LogMsgSrc}\n{LogMsg}", logMessage.Source, logMessage.Message);
		return Task.CompletedTask;
	}
}
