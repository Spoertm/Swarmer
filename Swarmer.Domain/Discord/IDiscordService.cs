using Discord;
using Swarmer.Domain.Data;

namespace Swarmer.Domain.Discord;

public interface IDiscordService
{
    ConnectionState GetConnectionState();

    Task<IUserMessage?> SendEmbedAsync(ulong channelId, Embed embed);

    Task GoOfflineAsync(StreamMessage streamMessage);

    Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream);
}
