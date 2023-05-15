using Discord;
using Swarmer.Domain.Models;
using Swarmer.Domain.Models.Database;

namespace Swarmer.Domain.Services;

public interface IDiscordService
{
	Task<Result<IUserMessage>> SendEmbedAsync(ulong channelId, Embed embed);

	Task GoOfflineAsync(StreamMessage streamMessage);

	Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream);
}
