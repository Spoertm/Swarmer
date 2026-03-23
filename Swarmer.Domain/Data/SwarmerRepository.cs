using Microsoft.EntityFrameworkCore;
using Swarmer.Domain.Discord;
using Swarmer.Domain.Twitch;

namespace Swarmer.Domain.Data;

public sealed class SwarmerRepository
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

    public async Task<IEnumerable<StreamToPost>> GetStreamsToPostAsync()
    {
        List<GameChannel> gameChannels = await _appDbContext.GameChannels
            .AsNoTracking()
            .ToListAsync();

        List<StreamMessage> streamMessages = await _appDbContext.StreamMessages
            .AsNoTracking()
            .ToListAsync();

        List<string> bannedUserLogins = await _appDbContext.BannedUsers
            .AsNoTracking()
            .Select(b => b.UserLogin)
            .ToListAsync();

        return from channel in gameChannels
               join stream in _streamProvider.Streams ?? [] on channel.TwitchGameId.ToString() equals stream.GameId
               where !bannedUserLogins.Contains(stream.UserLogin)
               where !streamMessages.Any(streamMessage =>
                   streamMessage.StreamId == stream.UserId && streamMessage.ChannelId == channel.StreamChannelId)
               select new StreamToPost(stream, channel);
    }

    public async Task UpdateLingeringStreamMessages(TimeSpan maxLingerTime)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;

        foreach (StreamMessage sm in await _appDbContext.StreamMessages.ToListAsync())
        {
            if (utcNow - sm.LingeringSinceUtc >= maxLingerTime)
            {
                sm.StopLingering();
            }
        }

        await SaveChangesAsync();
    }

    public async Task HandleExistingStreamsAsync(TimeSpan cooldownPeriod)
    {
        // Provider hasn't initialized Streams yet
        if (_streamProvider.Streams is null)
        {
            return;
        }

        DateTimeOffset utcNow = DateTimeOffset.UtcNow;

        foreach (StreamMessage streamMessage in await _appDbContext.StreamMessages.ToListAsync())
        {
            Stream? ongoingStream = Array.Find(_streamProvider.Streams, s => s.UserId == streamMessage.StreamId);

            // Stream is live on Twitch
            if (ongoingStream is not null)
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

                // Cooldown check: don't go online again if we're still within the cooldown period
                // This prevents Discord message flip-flopping due to Twitch API inconsistency
                if (utcNow - streamMessage.LingeringSinceUtc < cooldownPeriod)
                {
                    continue;
                }

                await _discordService.GoOnlineAgainAsync(streamMessage, ongoingStream);
                streamMessage.IsLive = true;
                streamMessage.Linger();
            }

            // Stream is offline on Twitch
            else
            {
                // The Discord message is live (stream just went offline)
                if (streamMessage.IsLive)
                {
                    // Cooldown check: don't go offline if we're still within the cooldown period
                    // This prevents Discord message flip-flopping due to Twitch API inconsistency
                    if (utcNow - streamMessage.LingeringSinceUtc < cooldownPeriod)
                    {
                        continue;
                    }

                    await _discordService.GoOfflineAsync(streamMessage);
                    streamMessage.IsLive = false;
                    streamMessage.Linger();
                }

                // The Discord message is offline, and it's lingering
                if (streamMessage.IsLingering)
                {
                    continue;
                }

                await RemoveStreamMessageAsync(streamMessage);
            }

            // Wait 1s between actions to not get rate-limited by Discord's API
            await Task.Delay(TimeSpan.FromSeconds(1));
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
