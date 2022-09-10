using Discord;
using Discord.Commands;
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
		const GatewayIntents gatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents;
		Client = new(new() { GatewayIntents = gatewayIntents});
		Client.Log += OnLog;
		Client.Ready += () =>
		{
			Client.MessageReceived += message => Task.Run(() => ClientOnMessageReceived(message));
			return Task.CompletedTask;
		};
	}

	private async Task ClientOnMessageReceived(SocketMessage msg)
	{
		if (msg is not SocketUserMessage { Source: MessageSource.User } message)
		{
			return;
		}

		int argumentPos = 0;
		if (!message.HasMentionPrefix(Client.CurrentUser, ref argumentPos))
		{
			return;
		}

		if (Emote.TryParse("<a:swarmer:855162753093337109>", out Emote swarmerEmote))
		{
			await msg.AddReactionAsync(swarmerEmote);
		}
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
