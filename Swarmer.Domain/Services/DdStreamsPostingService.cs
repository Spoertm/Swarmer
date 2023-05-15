﻿global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Swarmer.Domain.Models;
using Swarmer.Domain.Models.Database;
using Swarmer.Domain.Models.Extensions;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Domain.Services;

public sealed class DdStreamsPostingService : AbstractBackgroundService
{
	private readonly StreamProvider _streamProvider;
	private readonly SwarmerDiscordClient _discordClient;
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly ITwitchAPI _twitchApi;
	private readonly string[] _bannedUserLogins;

	public DdStreamsPostingService(
		IConfiguration config,
		SwarmerDiscordClient discordClient,
		IServiceScopeFactory serviceScopeFactory,
		ITwitchAPI twitchApi,
		StreamProvider streamProvider)
	{
		_discordClient = discordClient;
		_serviceScopeFactory = serviceScopeFactory;
		_twitchApi = twitchApi;
		_streamProvider = streamProvider;
		_bannedUserLogins = config.GetSection("BannedUserLogins").Get<string[]>() ?? Array.Empty<string>();
	}

	protected override TimeSpan Interval => TimeSpan.FromSeconds(15);
	public TimeSpan MaxLingeringTime { get; } = TimeSpan.FromMinutes(15);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		// Provider hasn't initialised Streams yet or token is cancelled
		if (_streamProvider.Streams is null || stoppingToken.IsCancellationRequested)
		{
			return;
		}

		await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
		SwarmerRepository repo = scope.ServiceProvider.GetRequiredService<SwarmerRepository>();

		await repo.UpdateLingeringStreamMessages(MaxLingeringTime);

		await PostCompletelyNewStreamsAndAddToDb(repo);

		await HandleStreamMessages(repo);
	}

	public async Task PostCompletelyNewStreamsAndAddToDb(SwarmerRepository repo)
	{
		// Provider hasn't initialised Streams yet
		if (_streamProvider.Streams is not { Length: > 0 })
		{
			return;
		}

		IEnumerable<StreamToPost> streamsToPost = repo.GetStreamsToPost();

		streamsToPost = streamsToPost.Where(stp => !_bannedUserLogins.Contains(stp.Stream.UserLogin));

		foreach (StreamToPost stp in streamsToPost)
		{
			User twitchUser = (await _twitchApi.Helix.Users.GetUsersAsync(ids: new() { stp.Stream.UserId })).Users[0];
			Embed newStreamEmbed = new EmbedBuilder().Online(stp.Stream, twitchUser.ProfileImageUrl);

			if (await _discordClient.GetChannelAsync(stp.Channel.StreamChannelId) is not ITextChannel channel)
			{
				Log.Error("Registered channel {@StreamChannel} doesn't exist", stp.Channel);
				continue;
			}

			bool canSendInChannel = (await channel.GetUserAsync(_discordClient.CurrentUser.Id)).GetPermissions(channel).SendMessages;
			if (!canSendInChannel)
			{
				continue;
			}

			IUserMessage message = await channel.SendMessageAsync(embed: newStreamEmbed);
			StreamMessage newDbStreamMessage = new()
			{
				StreamId = stp.Stream.UserId,
				IsLive = true,
				ChannelId = channel.Id,
				MessageId = message.Id,
				AvatarUrl = string.IsNullOrWhiteSpace(twitchUser.ProfileImageUrl) ? null : twitchUser.ProfileImageUrl,
				OfflineThumbnailUrl = string.IsNullOrWhiteSpace(twitchUser.OfflineImageUrl) ? null : twitchUser.OfflineImageUrl,
				LingeringSinceUtc = DateTime.UtcNow,
			};

			await repo.InsertStreamMessage(newDbStreamMessage);

			await Task.Delay(TimeSpan.FromSeconds(1)); // Wait 1s between actions to not get rate-limited by Discord's API
		}

		await repo.SaveChangesAsync();
	}

	public async Task HandleStreamMessages(SwarmerRepository repo)
	{
		// Provider hasn't initialised Streams yet
		if (_streamProvider.Streams is null)
		{
			return;
		}

		foreach (StreamMessage streamMessage in repo.GetStreamMessages())
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
					repo.RemoveStreamMessage(streamMessage);
					continue;
				}

				await GoOnlineAgainAsync(streamMessage, ongoingStream);
				streamMessage.IsLive = true;
				streamMessage.Linger();
			}
			else // Stream is offline on Twitch
			{
				if (streamMessage.IsLive) // The Discord message is live (stream just went offline)
				{
					await GoOfflineAsync(streamMessage);
					streamMessage.IsLive = false;
					streamMessage.Linger();
				}

				if (streamMessage.IsLingering) // The Discord message is offline, and it's lingering
				{
					continue;
				}

				repo.RemoveStreamMessage(streamMessage);
			}

			await Task.Delay(TimeSpan.FromSeconds(1)); // Wait 1s between actions to not get rate-limited by Discord's API
		}

		await repo.SaveChangesAsync();
	}

	private async Task GoOfflineAsync(StreamMessage streamMessage)
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

	private async Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream)
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
