global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using Swarmer.Domain.Data;
using Swarmer.Domain.Discord;
using Swarmer.Domain.Extensions;
using Swarmer.Domain.Models;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Domain.Twitch;

public sealed class StreamsPostingService(
    IServiceScopeFactory serviceScopeFactory,
    ITwitchAPI twitchApi,
    StreamProvider streamProvider,
    IDiscordService discordService) : RepeatingBackgroundService
{
    private readonly TimeSpan _maxLingeringTime = TimeSpan.FromMinutes(15);

    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
    {
        // Provider hasn't initialised Streams yet or token is cancelled
        if (streamProvider.Streams is null || stoppingToken.IsCancellationRequested)
        {
            return;
        }

        // Discord client is not ready
        if (discordService.GetConnectionState() != ConnectionState.Connected)
        {
            return;
        }

        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        SwarmerRepository repo = scope.ServiceProvider.GetRequiredService<SwarmerRepository>();

        await repo.UpdateLingeringStreamMessages(_maxLingeringTime);

        await PostCompletelyNewStreamsAndAddToDb(repo);

        await repo.HandleExistingStreamsAsync(_maxLingeringTime);
    }

    private async Task PostCompletelyNewStreamsAndAddToDb(SwarmerRepository repo)
    {
        // Provider hasn't initialized Streams yet
        if (streamProvider.Streams is not { Length: > 0 })
        {
            return;
        }

        foreach (StreamToPost stp in await repo.GetStreamsToPostAsync())
        {
            User twitchUser = (await twitchApi.Helix.Users.GetUsersAsync([stp.Stream.UserId])).Users[0];
            Embed newStreamEmbed = new EmbedBuilder().Online(stp.Stream, twitchUser.ProfileImageUrl);

            IUserMessage? message =
                await discordService.SendEmbedAsync(stp.Channel.StreamChannelId, newStreamEmbed);
            if (message is null)
            {
                continue;
            }

            StreamMessage newDbStreamMessage = new()
            {
                StreamId = stp.Stream.UserId,
                IsLive = true,
                ChannelId = stp.Channel.StreamChannelId,
                MessageId = message.Id,
                AvatarUrl =
                    string.IsNullOrWhiteSpace(twitchUser.ProfileImageUrl) ? null : twitchUser.ProfileImageUrl,
                OfflineThumbnailUrl =
                    string.IsNullOrWhiteSpace(twitchUser.OfflineImageUrl) ? null : twitchUser.OfflineImageUrl,
                LingeringSinceUtc = DateTime.UtcNow,
            };

            await repo.InsertStreamMessageAsync(newDbStreamMessage);

            await Task.Delay(TimeSpan
                .FromSeconds(1)); // Wait 1s between actions to not get rate-limited by Discord's API
        }

        await repo.SaveChangesAsync();
    }
}
