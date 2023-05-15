using Microsoft.EntityFrameworkCore;
using Swarmer.Domain.Models;
using Swarmer.Domain.Models.Database;

namespace Swarmer.Domain.Services;

public class SwarmerRepository
{
	private readonly StreamProvider _streamProvider;
	private readonly AppDbContext _appDbContext;

	public SwarmerRepository(StreamProvider streamProvider, AppDbContext appDbContext)
	{
		_streamProvider = streamProvider;
		_appDbContext = appDbContext;
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
		foreach (StreamMessage ddStream in _appDbContext.StreamMessages)
		{
			bool hasLingeredForLongEnough = ddStream.LingeringSinceUtc.HasValue && utcNow - ddStream.LingeringSinceUtc >= maxLingerTime;
			if (hasLingeredForLongEnough)
			{
				ddStream.StopLingering();
			}
		}

		await _appDbContext.SaveChangesAsync();
	}

	public DbSet<StreamMessage> GetStreamMessages()
		=> _appDbContext.StreamMessages;

	public async Task InsertStreamMessage(StreamMessage streamMessage)
		=> await _appDbContext.StreamMessages.AddAsync(streamMessage);

	public void RemoveStreamMessage(StreamMessage streamMessage)
		=> _appDbContext.StreamMessages.Remove(streamMessage);

	public async Task SaveChangesAsync()
		=> await _appDbContext.SaveChangesAsync();
}
