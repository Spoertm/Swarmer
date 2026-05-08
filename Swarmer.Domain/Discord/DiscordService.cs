using Discord;
using Serilog;
using Swarmer.Domain.Data;
using Swarmer.Domain.Extensions;

namespace Swarmer.Domain.Discord;

public sealed class DiscordService : IDiscordService
{
    private readonly SwarmerDiscordClient _discordClient;

    public DiscordService(SwarmerDiscordClient discordClient) => _discordClient = discordClient;

    public ConnectionState GetConnectionState() => _discordClient.ConnectionState;

    public async Task<IUserMessage?> SendEmbedAsync(ulong channelId, Embed embed)
    {
        if (await _discordClient.GetChannelAsync(channelId) is not ITextChannel channel)
        {
            Log.Error("Registered channel with ID {ChannelId} doesn't exist", channelId);
            return null;
        }

        bool canSendInChannel = (await channel.GetUserAsync(_discordClient.CurrentUser.Id)).GetPermissions(channel)
            .SendMessages;
        if (!canSendInChannel)
        {
            Log.Error("Lacking permissions to send messages in channel {ChannelId}", channelId);
            return null;
        }

        return await channel.SendMessageAsync(embed: embed);
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
        await message.ModifyAsync(m => m.Embeds = new Optional<Embed[]>([newEmbed]));
    }

    public async Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream)
    {
        if (await _discordClient.GetChannelAsync(streamMessage.ChannelId) is not ITextChannel channel ||
            await channel.GetMessageAsync(streamMessage.MessageId) is not IUserMessage message)
        {
            return;
        }

        Embed streamEmbed = new EmbedBuilder().Online(ongoingStream, streamMessage.AvatarUrl);
        await message.ModifyAsync(m => m.Embeds = new Optional<Embed[]>([streamEmbed]));
    }
}
