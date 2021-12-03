using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Swarmer.Models;
using System;
using System.Threading.Tasks;

namespace Swarmer.Services;

public class MessageHandlerService
{
	private readonly DiscordSocketClient _client;
	private readonly CommandService _commands;
	private readonly IServiceProvider _services;
	private readonly Config _config;

	public MessageHandlerService(DiscordSocketClient client, CommandService commands, IServiceProvider services, Config config)
	{
		_client = client;
		_commands = commands;
		_services = services;
		_config = config;

		client.MessageReceived += OnMessageRecievedAsync;
	}

	private async Task OnMessageRecievedAsync(SocketMessage msg)
	{
		if (msg is not SocketUserMessage { Source: MessageSource.User } message)
			return;

		if (message.Content.Trim() == _client.CurrentUser.Mention && Emote.TryParse(_config.ReactionEmote, out Emote emote))
		{
			await message.AddReactionAsync(emote);
			return;
		}

		int argumentPos = 0;
		if (message.HasStringPrefix(_config.Prefix, ref argumentPos) || message.HasMentionPrefix(_client.CurrentUser, ref argumentPos))
		{
			SocketCommandContext context = new(_client, message);
			await _commands.ExecuteAsync(context, argumentPos, _services);
		}
	}
}