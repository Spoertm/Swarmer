using Discord;
using Swarmer.Domain.Data;
using Swarmer.Domain.Models;

namespace Swarmer.Domain.Discord;

public interface IDiscordService
{
    ConnectionState GetConnectionState();

    Task<Result<IUserMessage>> SendEmbedAsync(ulong channelId, Embed embed);

    Task GoOfflineAsync(StreamMessage streamMessage);

    Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream);
}
