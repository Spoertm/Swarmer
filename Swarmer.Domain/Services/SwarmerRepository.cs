using Microsoft.EntityFrameworkCore;
using Swarmer.Domain.Models;
using Swarmer.Domain.Models.Database;

namespace Swarmer.Domain.Services;

public class SwarmerRepository
{
	private readonly StreamProvider _streamProvider;
	private readonly AppDbContext _appDbContext;
	private readonly IDiscordService _discordService;

	public SwarmerRepository(
		AppDbContext appDbContext,
		StreamProvider streamProvider,
		IDiscordService discordService)
	{
		_streamProvider = streamProvider;
		_appDbContext = appDbContext;
		_discordService = discordService;
	}

	public IEnumerable<StreamToPost> GetStreamsToPost()
	{
		return from channel in _appDbContext.GameChannels.ToList()
				join stream in _streamProvider.Streams on channel.TwitchGameId.ToString() equals stream.GameId
				where !_appDbContext.StreamMessages.Any(os => os.StreamId == stream.UserId && os.ChannelId == channel.StreamChannelId)
				select new StreamToPost(stream, channel);
	}

	public async Task UpdateLingeringStreamMessages(TimeSpan maxLingerTime)
	{
		DateTime utcNow = DateTime.UtcNow;

		await _appDbContext.StreamMessages
			.Where(sm => utcNow - sm.LingeringSinceUtc >= maxLingerTime)
			.ForEachAsync(sm => sm.StopLingering());

		await SaveChangesAsync();
	}

	public async Task HandleExistingStreamsAsync()
	{
		// Provider hasn't initialised Streams yet
		if (_streamProvider.Streams is null)
		{
			return;
		}

		foreach (StreamMessage streamMessage in _appDbContext.StreamMessages.AsQueryable())
		{
			Stream? ongoingStream = Array.Find(_streamProvider.Streams, s => s.UserId == streamMessage.StreamId);
			if (ongoingStream is not null) // Stream is live on Twitch
			{
				if (streamMessage.IsLive)
				{
					continue;
				}

				if (!streamMessage.IsLingering)
				{
					await RemoveStreamMessageAsync(streamMessage);
					continue;
				}

				await _discordService.GoOnlineAgainAsync(streamMessage, ongoingStream);
				streamMessage.IsLive = true;
				streamMessage.Linger();
			}
			else // Stream is offline on Twitch
			{
				if (streamMessage.IsLive) // The Discord message is live (stream just went offline)
				{
					await _discordService.GoOfflineAsync(streamMessage);
					streamMessage.IsLive = false;
					streamMessage.Linger();
				}

				if (streamMessage.IsLingering) // The Discord message is offline, and it's lingering
				{
					continue;
				}

				await RemoveStreamMessageAsync(streamMessage);
			}

			await Task.Delay(TimeSpan.FromSeconds(1)); // Wait 1s between actions to not get rate-limited by Discord's API
		}

		await SaveChangesAsync();
	}

	public async Task InsertStreamMessageAsync(StreamMessage streamMessage)
	{
		await _appDbContext.StreamMessages.AddAsync(streamMessage);
		await SaveChangesAsync();
	}

	public async Task RemoveStreamMessageAsync(StreamMessage streamMessage)
	{
		_appDbContext.StreamMessages.Remove(streamMessage);
		await SaveChangesAsync();
	}

	public async Task SaveChangesAsync()
		=> await _appDbContext.SaveChangesAsync();
}
