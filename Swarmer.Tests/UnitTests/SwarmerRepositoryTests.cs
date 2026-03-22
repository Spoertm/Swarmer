using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Swarmer.Domain.Database;
using Swarmer.Domain.Discord;
using Swarmer.Domain.Twitch;
using Xunit;

namespace Swarmer.Tests.UnitTests;

public sealed class SwarmerRepositoryTests
{
	private readonly DbContextOptions<AppDbContext> _dbContextOptions;
	private readonly IDiscordService _discordServiceMock;

	public SwarmerRepositoryTests()
	{
		Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

		_dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		IDiscordService discordServiceMock = Substitute.For<IDiscordService>();
		discordServiceMock
			.GoOnlineAgainAsync(Arg.Any<StreamMessage>(), Arg.Any<MockStream>())
			.Returns(Task.CompletedTask);

		discordServiceMock
			.GoOfflineAsync(Arg.Any<StreamMessage>())
			.Returns(Task.CompletedTask);

		_discordServiceMock = discordServiceMock;
	}

	[Fact]
	public async Task GetStreamsToPost_ReturnsExpectedStreams()
	{
		await using AppDbContext appDbContext = new(_dbContextOptions);

		GameChannel gameChannel1 = new() { TwitchGameId = 1, StreamChannelId = 12345 };
		GameChannel gameChannel2 = new() { TwitchGameId = 2, StreamChannelId = 54321 };
		StreamMessage streamMessage = new() { Id = 1, StreamId = "userid1", ChannelId = 12345 };

		MockStream stream1 = new("1", "userid1", gameId: "1");
		MockStream stream2 = new("2", "userid2", gameId: "1");
		MockStream bannedUserStream = new("3", "userid3", "SomeBannedLogin", gameId: "1");
		StreamProvider streamProvider = new() { Streams = [stream1, stream2, bannedUserStream] };

		appDbContext.GameChannels.AddRange(gameChannel1, gameChannel2);
		appDbContext.StreamMessages.Add(streamMessage);
		appDbContext.BannedUsers.Add(new BannedUser { UserLogin = "SomeBannedLogin" });
		await appDbContext.SaveChangesAsync();

		SwarmerRepository sut = new(appDbContext, streamProvider, _discordServiceMock);
		List<StreamToPost> result = (await sut.GetStreamsToPostAsync()).ToList();

		Assert.Single(result);
		Assert.Equal(stream2, result[0].Stream);
	}

	[Fact]
	public async Task UpdateLingeringStreamMessages_UpdatesLingeringStreams()
	{
		await using AppDbContext appDbContext = new(_dbContextOptions);

		SwarmerRepository sut = new(appDbContext, new StreamProvider(), _discordServiceMock);

		TimeSpan maxLingerTime = TimeSpan.FromMinutes(15);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		StreamMessage lingeringStream1 = new() { Id = 1, StreamId = "", LingeringSinceUtc = now - maxLingerTime };
		StreamMessage lingeringStream2 = new() { Id = 2, StreamId = "", LingeringSinceUtc = now - maxLingerTime.Subtract(TimeSpan.FromMinutes(5)) };
		StreamMessage recentStream = new() { Id = 3, StreamId = "", LingeringSinceUtc = null };

		appDbContext.StreamMessages.AddRange(lingeringStream1, lingeringStream2, recentStream);
		await appDbContext.SaveChangesAsync();

		await sut.UpdateLingeringStreamMessages(maxLingerTime);

		Assert.False((await appDbContext.StreamMessages.FindAsync(1))!.IsLingering);
		Assert.True((await appDbContext.StreamMessages.FindAsync(2))!.IsLingering);
		Assert.Null((await appDbContext.StreamMessages.FindAsync(3))!.LingeringSinceUtc);
	}

	[Fact]
	public async Task HandleExistingStreamsAsync_StreamsProviderNotInitialized_ReturnsImmediately()
	{
		AppDbContext appDbContextMock = Substitute.For<AppDbContext>();
		StreamProvider streamProvider = new();

		SwarmerRepository repository =
			new(appDbContextMock, streamProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);

		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		// Make sure that the method returns immediately when the stream provider is not initialized
		await appDbContextMock.DidNotReceive().StreamMessages.AddAsync(Arg.Any<StreamMessage>());
		await _discordServiceMock.DidNotReceive().GoOnlineAgainAsync(Arg.Any<StreamMessage>(), Arg.Any<MockStream>());
		await _discordServiceMock.DidNotReceive().GoOfflineAsync(Arg.Any<StreamMessage>());
	}

	[Fact]
	public async Task HandleExistingStreamsAsync_StreamIsLiveOnTwitch_DiscordMessageIsLive_DoesNothing()
	{
		const string streamId = "SomeId";
		StreamMessage streamMessageBefore = new() { Id = 1, StreamId = streamId, IsLive = true };
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessageBefore);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = [stream] };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);

		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		Assert.Single(appDbContext.StreamMessages.ToList());
		StreamMessage? streamMessageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.Equal(streamMessageBefore, streamMessageAfter);
		await _discordServiceMock.DidNotReceive().GoOnlineAgainAsync(Arg.Any<StreamMessage>(), Arg.Any<MockStream>());
		await _discordServiceMock.DidNotReceive().GoOfflineAsync(Arg.Any<StreamMessage>());
	}

	[Fact]
	public async Task
		HandleExistingStreamsAsync_StreamIsLiveOnTwitch_DiscordMessageIsNotLiveOrLingering_RemovesStreamMessage()
	{
		const string streamId = "SomeId";
		StreamMessage streamMessage = new() { Id = 0, StreamId = streamId, IsLive = false, LingeringSinceUtc = null };
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = [stream] };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);

		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		Assert.Empty(appDbContext.StreamMessages.ToList());
	}

	[Fact]
	public async Task
		HandleExistingStreamsAsync_StreamIsLiveOnTwitch_DiscordMessageIsNotLiveAndLingering_UpdatesAndLingersMessage()
	{
		const string streamId = "SomeId";
		// Stream went offline 16 minutes ago (cooldown expired), so we can bring it back online
		DateTimeOffset lingeringSince = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(16);
		StreamMessage streamMessage = new() { Id = 1, StreamId = streamId, IsLive = false, LingeringSinceUtc = lingeringSince };
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = [stream] };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);

		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		await _discordServiceMock.Received(1).GoOnlineAgainAsync(streamMessage, stream);
		StreamMessage? streamMessageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.True(streamMessageAfter?.IsLingering);
		Assert.True(streamMessageAfter?.IsLive);
	}

	[Fact]
	public async Task HandleExistingStreamsAsync_StreamIsOfflineOnTwitch_DiscordMessageIsLive_UpdatesAndLingersMessage()
	{
		const string streamId = "SomeId";
		StreamMessage streamMessage = new() { Id = 1, StreamId = streamId, IsLive = true };

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = [] };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);

		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		await _discordServiceMock.Received(1).GoOfflineAsync(streamMessage);
		StreamMessage? streamMessageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.False(streamMessageAfter?.IsLive);
		Assert.True(streamMessageAfter?.IsLingering);
	}

	[Fact]
	public async Task
		HandleExistingStreamsAsync_StreamIsOfflineOnTwitch_DiscordMessageIsOfflineAndLingering_DoesNothing()
	{
		const string streamId = "SomeId";
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		StreamMessage streamMessageBefore =
			new() { Id = 1, StreamId = streamId, IsLive = false, LingeringSinceUtc = utcNow };

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessageBefore);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = [] };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);

		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		Assert.Single(appDbContext.StreamMessages.ToList());
		StreamMessage? streamMessageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.Equal(streamMessageBefore, streamMessageAfter);
		await _discordServiceMock.DidNotReceive().GoOnlineAgainAsync(Arg.Any<StreamMessage>(), Arg.Any<MockStream>());
		await _discordServiceMock.DidNotReceive().GoOfflineAsync(Arg.Any<StreamMessage>());
	}

	[Fact]
	public async Task
		HandleExistingStreamsAsync_StreamIsOfflineOnTwitch_DiscordMessageIsNotLiveOrLingering_RemovesStreamMessage()
	{
		const string streamId = "SomeId";
		StreamMessage streamMessageBefore = new() { Id = 0, StreamId = streamId, IsLive = false, LingeringSinceUtc = null };

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessageBefore);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = [] };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);

		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		Assert.Empty(appDbContext.StreamMessages.ToList());
	}

	[Fact]
	public async Task InsertStreamMessageAsync_InsertsStreamMessage()
	{
		await using AppDbContext appDbContext = new(_dbContextOptions);

		SwarmerRepository repository = new(appDbContext, new StreamProvider(), _discordServiceMock);

		StreamMessage streamMessage = new() { Id = 0, StreamId = "SomeId" };
		await repository.InsertStreamMessageAsync(streamMessage);

		Assert.Single(appDbContext.StreamMessages.ToList());
	}

	[Fact]
	public async Task RemoveStreamMessageAsync_RemovesStreamMessage()
	{
		await using AppDbContext appDbContext = new(_dbContextOptions);

		StreamMessage streamMessage = new() { Id = 0, StreamId = "SomeId" };
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		SwarmerRepository repository = new(appDbContext, new StreamProvider(), _discordServiceMock);
		await repository.RemoveStreamMessageAsync(streamMessage);

		Assert.Empty(appDbContext.StreamMessages.ToList());
	}

	/// <summary>
	/// Tests that the symmetric cooldown prevents Discord message flip-flopping when Twitch API is inconsistent.
	/// When a stream flaps (online?offline?online rapidly), the Discord message should NOT be modified
	/// until the cooldown period has passed.
	/// </summary>
	[Fact]
	public async Task HandleExistingStreamsAsync_RapidStreamFlapping_CooldownPreventsDiscordModifications()
	{
		const string streamId = "SomeId";
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);

		// Initial state: Stream is live on both Twitch and Discord (posted just now)
		DateTimeOffset initialLingeringTime = DateTimeOffset.UtcNow;
		StreamMessage streamMessage = new()
		{
			Id = 1,
			StreamId = streamId,
			IsLive = true,
			LingeringSinceUtc = initialLingeringTime
		};
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);

		// Simulate the stream flapping 3 times (online?offline?online?offline?online)
		// Each cycle represents Twitch API inconsistency

		// Cycle 1: Stream goes offline (Twitch API doesn't return it)
		StreamProvider offlineProvider1 = new() { Streams = [] };
		SwarmerRepository repository1 = new(appDbContext, offlineProvider1, _discordServiceMock);
		await repository1.HandleExistingStreamsAsync(cooldownPeriod);

		// With cooldown: GoOfflineAsync should NOT be called (we're within cooldown)
		await _discordServiceMock.DidNotReceive().GoOfflineAsync(Arg.Is<StreamMessage>(sm => sm.StreamId == streamId));
		StreamMessage? messageAfterOffline1 = await appDbContext.StreamMessages.FindAsync(1);
		Assert.True(messageAfterOffline1?.IsLive); // Message should still show as live
		Assert.True(messageAfterOffline1?.IsLingering);
		Assert.Equal(initialLingeringTime, messageAfterOffline1!.LingeringSinceUtc); // Timestamp unchanged

		// Cycle 1: Stream comes back online
		StreamProvider onlineProvider1 = new() { Streams = [stream] };
		SwarmerRepository repository2 = new(appDbContext, onlineProvider1, _discordServiceMock);
		await repository2.HandleExistingStreamsAsync(cooldownPeriod);

		// GoOnlineAgainAsync should NOT be called (message was never set to offline due to cooldown)
		await _discordServiceMock.DidNotReceive().GoOnlineAgainAsync(Arg.Is<StreamMessage>(sm => sm.StreamId == streamId), stream);
		StreamMessage? messageAfterOnline1 = await appDbContext.StreamMessages.FindAsync(1);
		Assert.True(messageAfterOnline1?.IsLive); // Still live
		Assert.True(messageAfterOnline1?.IsLingering);
		Assert.Equal(initialLingeringTime, messageAfterOnline1!.LingeringSinceUtc); // Timestamp unchanged

		// Cycle 2: Stream goes offline again
		StreamProvider offlineProvider2 = new() { Streams = [] };
		SwarmerRepository repository3 = new(appDbContext, offlineProvider2, _discordServiceMock);
		await repository3.HandleExistingStreamsAsync(cooldownPeriod);

		// GoOfflineAsync should still NOT be called (still within cooldown)
		await _discordServiceMock.DidNotReceive().GoOfflineAsync(Arg.Is<StreamMessage>(sm => sm.StreamId == streamId));

		// Cycle 2: Stream comes back online again
		StreamProvider onlineProvider2 = new() { Streams = [stream] };
		SwarmerRepository repository4 = new(appDbContext, onlineProvider2, _discordServiceMock);
		await repository4.HandleExistingStreamsAsync(cooldownPeriod);

		// GoOnlineAgainAsync should still NOT be called
		await _discordServiceMock.DidNotReceive().GoOnlineAgainAsync(Arg.Is<StreamMessage>(sm => sm.StreamId == streamId), stream);

		// Final verification:
		// 1. GoOfflineAsync was called 0 times (cooldown prevented it)
		// 2. GoOnlineAgainAsync was called 0 times (message never went offline)
		// 3. The Discord message was never modified despite Twitch API inconsistency
		// 4. The message still shows as "live" throughout the entire flapping period
		StreamMessage? finalMessage = await appDbContext.StreamMessages.FindAsync(1);
		Assert.True(finalMessage?.IsLive);
		Assert.Equal(initialLingeringTime, finalMessage?.LingeringSinceUtc);
	}

	/// <summary>
	/// Tests the symmetric cooldown behavior: when a stream comes back online within the cooldown period,
	/// the Discord message should stay in its current state and NOT be modified.
	/// </summary>
	[Fact]
	public async Task HandleExistingStreamsAsync_StreamReturnsWithinCooldown_DoesNotModifyDiscordMessage()
	{
		const string streamId = "SomeId";
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);

		// Initial state: Stream is live on both Twitch and Discord (posted 5 min ago)
		DateTimeOffset initialLingeringTime = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
		StreamMessage streamMessage = new()
		{
			Id = 1,
			StreamId = streamId,
			IsLive = true,
			LingeringSinceUtc = initialLingeringTime
		};
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		// Stream goes offline (within cooldown)
		StreamProvider offlineProvider = new() { Streams = [] };
		SwarmerRepository repository1 = new(appDbContext, offlineProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);
		await repository1.HandleExistingStreamsAsync(cooldownPeriod);

		// IDEAL BEHAVIOR: GoOfflineAsync should NOT be called because we're within cooldown
		await _discordServiceMock.DidNotReceive().GoOfflineAsync(Arg.Is<StreamMessage>(sm => sm.StreamId == streamId));
		StreamMessage? messageAfterOffline = await appDbContext.StreamMessages.FindAsync(1);
		Assert.True(messageAfterOffline?.IsLive); // Should still show as live
		Assert.True(messageAfterOffline?.IsLingering);

		// Stream comes back online (still within cooldown)
		StreamProvider onlineProvider = new() { Streams = [stream] };
		SwarmerRepository repository2 = new(appDbContext, onlineProvider, _discordServiceMock);
		await repository2.HandleExistingStreamsAsync(cooldownPeriod);

		// IDEAL BEHAVIOR: GoOnlineAgainAsync should NOT be called (message never went offline)
		await _discordServiceMock.DidNotReceive().GoOnlineAgainAsync(Arg.Is<StreamMessage>(sm => sm.StreamId == streamId), stream);

		StreamMessage? messageAfterOnline = await appDbContext.StreamMessages.FindAsync(1);
		Assert.True(messageAfterOnline?.IsLive); // Should still be live
		Assert.True(messageAfterOnline?.IsLingering);
	}

	/// <summary>
	/// Tests symmetric cooldown: stream goes online then immediately offline within cooldown.
	/// The Discord message should NOT be modified during the cooldown period.
	/// </summary>
	[Fact]
	public async Task HandleExistingStreamsAsync_StreamGoesOnlineThenOfflineWithinCooldown_DoesNotModifyMessage()
	{
		const string streamId = "SomeId";
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);

		// Initial state: Stream just went live 2 minutes ago (within cooldown)
		DateTimeOffset initialLingeringTime = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2);
		StreamMessage streamMessage = new()
		{
			Id = 1,
			StreamId = streamId,
			IsLive = true,
			LingeringSinceUtc = initialLingeringTime
		};
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		// Stream goes offline within the cooldown period
		StreamProvider offlineProvider = new() { Streams = [] };
		SwarmerRepository repository = new(appDbContext, offlineProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);
		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		// IDEAL BEHAVIOR: GoOfflineAsync should NOT be called - cooldown prevents state change
		await _discordServiceMock.DidNotReceive().GoOfflineAsync(Arg.Any<StreamMessage>());

		StreamMessage? messageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.True(messageAfter?.IsLive); // Message should still show as live
		Assert.True(messageAfter?.IsLingering);
		// LingeringSinceUtc should NOT be reset (still the original time from when stream went live)
		Assert.Equal(initialLingeringTime, messageAfter?.LingeringSinceUtc);
	}

	/// <summary>
	/// Tests that state changes are allowed after the cooldown period expires.
	/// </summary>
	[Fact]
	public async Task HandleExistingStreamsAsync_StreamGoesOfflineAfterCooldown_UpdatesMessage()
	{
		const string streamId = "SomeId";
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);

		// Initial state: Stream went live 16 minutes ago (cooldown expired)
		DateTimeOffset initialLingeringTime = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(16);
		StreamMessage streamMessage = new()
		{
			Id = 1,
			StreamId = streamId,
			IsLive = true,
			LingeringSinceUtc = initialLingeringTime
		};
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		// Stream goes offline after cooldown period
		StreamProvider offlineProvider = new() { Streams = [] };
		SwarmerRepository repository = new(appDbContext, offlineProvider, _discordServiceMock);
		TimeSpan cooldownPeriod = TimeSpan.FromMinutes(15);
		await repository.HandleExistingStreamsAsync(cooldownPeriod);

		// IDEAL BEHAVIOR: GoOfflineAsync SHOULD be called - cooldown has expired
		await _discordServiceMock.Received(1).GoOfflineAsync(Arg.Is<StreamMessage>(sm => sm.StreamId == streamId));

		StreamMessage? messageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.False(messageAfter?.IsLive); // Message should now show as offline
		Assert.True(messageAfter?.IsLingering);
		// LingeringSinceUtc should be reset to now (when we actually went offline)
		Assert.True(messageAfter?.LingeringSinceUtc > initialLingeringTime);
	}
}

