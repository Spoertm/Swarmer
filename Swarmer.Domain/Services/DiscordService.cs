using Discord;
using Serilog;
using Swarmer.Domain.Models;
using Swarmer.Domain.Models.Database;
using Swarmer.Domain.Models.Extensions;

namespace Swarmer.Domain.Services;

public class DiscordService : IDiscordService
{
	private readonly SwarmerDiscordClient _discordClient;

	public DiscordService(SwarmerDiscordClient discordClient)
	{
		_discordClient = discordClient;
	}

	public async Task<Result<IUserMessage>> SendEmbedAsync(ulong channelId, Embed embed)
	{
		if (await _discordClient.GetChannelAsync(channelId) is not ITextChannel channel)
		{
			Log.Error("Registered channel with ID {ChannelId} doesn't exist", channelId);
			return Result.Failure<IUserMessage>($"Discord channel {channelId} doesn't exist.")!;
		}

		bool canSendInChannel = (await channel.GetUserAsync(_discordClient.CurrentUser.Id)).GetPermissions(channel).SendMessages;
		if (!canSendInChannel)
		{
			Log.Error("Lacking permissions to send messages in channel {ChannelId}", channelId);
			return Result.Failure<IUserMessage>($"Lacking permissions to send messages in channel {channelId}")!;
		}

		IUserMessage message = await channel.SendMessageAsync(embed: embed);
		return Result.Success(message);
	}

	public async Task GoOfflineAsync(StreamMessage streamMessage)
	{
		if (!streamMessage.IsLive ||
			await _discordClient.GetChannelAsync(streamMessage.ChannelId) is not ITextChannel channel ||
			await channel.GetMessageAsync(streamMessage.MessageId) is not IUserMessage message)
		{
			return;
		}

		Embed newEmbed = new EmbedBuilder().Offline(message.Embeds.First(), streamMessage.OfflineThumbnailUrl);
		await message.ModifyAsync(m => m.Embeds = new(new[] { newEmbed }));
	}

	public async Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream)
	{
		if (await _discordClient.GetChannelAsync(streamMessage.ChannelId) is not ITextChannel channel ||
			await channel.GetMessageAsync(streamMessage.MessageId) is not IUserMessage message)
		{
			return;
		}

		Embed streamEmbed = new EmbedBuilder().Online(ongoingStream, streamMessage.AvatarUrl);
		await message.ModifyAsync(m => m.Embeds = new(new[] { streamEmbed }));
	}
}
