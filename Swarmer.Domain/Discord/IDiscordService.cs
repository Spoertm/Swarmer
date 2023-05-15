using Discord;
using Swarmer.Domain.Database;
using Swarmer.Domain.Models;

namespace Swarmer.Domain.Discord;

public interface IDiscordService
{
	Task<Result<IUserMessage>> SendEmbedAsync(ulong channelId, Embed embed);

	Task GoOfflineAsync(StreamMessage streamMessage);

	Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream);
}
