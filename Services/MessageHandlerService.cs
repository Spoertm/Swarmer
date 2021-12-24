using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Swarmer.Services;

public class MessageHandlerService
{
	private readonly DiscordSocketClient _client;
	private readonly CommandService _commands;
	private readonly IServiceProvider _services;
	private readonly string _reactionEmote = Environment.GetEnvironmentVariable("ReactionEmote")!;
	private readonly string _prefix = Environment.GetEnvironmentVariable("Prefix")!;

	public MessageHandlerService(DiscordSocketClient client, CommandService commands, IServiceProvider services)
	{
		_client = client;
		_commands = commands;
		_services = services;

		client.MessageReceived += OnMessageRecievedAsync;
	}

	private async Task OnMessageRecievedAsync(SocketMessage msg)
	{
		if (msg is not SocketUserMessage { Source: MessageSource.User } message)
			return;

		if (message.Content.Trim() == _client.CurrentUser.Mention && Emote.TryParse(_reactionEmote, out Emote emote))
		{
			await message.AddReactionAsync(emote);
			return;
		}

		int argumentPos = 0;
		if (message.HasStringPrefix(_prefix, ref argumentPos) || message.HasMentionPrefix(_client.CurrentUser, ref argumentPos))
		{
			SocketCommandContext context = new(_client, message);
			await _commands.ExecuteAsync(context, argumentPos, _services);
		}
	}
}
