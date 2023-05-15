using Microsoft.EntityFrameworkCore;
using Moq;
using Swarmer.Domain.Database;
using Swarmer.Domain.Discord;
using Swarmer.Domain.Twitch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using Xunit;

namespace Swarmer.UnitTests;

public class SwarmerRepositoryTests
{
	private readonly DbContextOptions<AppDbContext> _dbContextOptions;
	private readonly Mock<IDiscordService> _discordServiceMock;

	public SwarmerRepositoryTests()
	{
		Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

		_dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;

		Mock<IDiscordService> discordServiceMock = new();
		discordServiceMock
			.Setup(d => d.GoOnlineAgainAsync(It.IsAny<StreamMessage>(), It.IsAny<MockStream>()))
			.Returns(Task.CompletedTask);

		discordServiceMock
			.Setup(d => d.GoOfflineAsync(It.IsAny<StreamMessage>()))
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
		StreamProvider streamProvider = new()
		{
			Streams = new Stream[] { stream1, stream2 },
		};

		appDbContext.GameChannels.AddRange(gameChannel1, gameChannel2);
		appDbContext.StreamMessages.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		SwarmerRepository sut = new(appDbContext, streamProvider, _discordServiceMock.Object);
		List<StreamToPost> result = sut.GetStreamsToPost().ToList();

		Assert.Single(result);
		Assert.Equal(stream2, result[0].Stream);
		Assert.Equal(gameChannel1, result[0].Channel);
	}

	[Fact]
	public async Task UpdateLingeringStreamMessages_UpdatesLingeringStreams()
	{
		await using AppDbContext appDbContext = new(_dbContextOptions);

		SwarmerRepository sut = new(appDbContext, new(), _discordServiceMock.Object);

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
		// Arrange
		Mock<AppDbContext> appDbContextMock = new();
		StreamProvider streamProvider = new();

		SwarmerRepository repository = new(appDbContextMock.Object, streamProvider, _discordServiceMock.Object);

		// Act
		await repository.HandleExistingStreamsAsync();

		// Assert
		// Make sure that the method returns immediately when the stream provider is not initialized
		appDbContextMock.VerifyNoOtherCalls();
		_discordServiceMock.VerifyNoOtherCalls();
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

		StreamProvider streamProvider = new() { Streams = new Stream[] { stream } };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock.Object);

		await repository.HandleExistingStreamsAsync();

		Assert.Single(appDbContext.StreamMessages.ToList());
		StreamMessage? streamMessageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.Equal(streamMessageBefore, streamMessageAfter);
		_discordServiceMock.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task HandleExistingStreamsAsync_StreamIsLiveOnTwitch_DiscordMessageIsNotLiveOrLingering_RemovesStreamMessage()
	{
		const string streamId = "SomeId";
		StreamMessage streamMessage = new() { Id = It.IsAny<int>(), StreamId = streamId, IsLive = false, LingeringSinceUtc = null };
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = new Stream[] { stream } };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock.Object);

		await repository.HandleExistingStreamsAsync();

		Assert.Empty(appDbContext.StreamMessages.ToList());
	}

	[Fact]
	public async Task HandleExistingStreamsAsync_StreamIsLiveOnTwitch_DiscordMessageIsNotLiveAndLingering_UpdatesAndLingersMessage()
	{
		const string streamId = "SomeId";
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		StreamMessage streamMessage = new() { Id = 1, StreamId = streamId, IsLive = false, LingeringSinceUtc = utcNow };
		MockStream stream = new("1", streamId);

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = new Stream[] { stream } };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock.Object);

		await repository.HandleExistingStreamsAsync();

		_discordServiceMock.Verify(ds => ds.GoOnlineAgainAsync(streamMessage, stream));
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

		StreamProvider streamProvider = new() { Streams = Array.Empty<Stream>() };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock.Object);

		await repository.HandleExistingStreamsAsync();

		_discordServiceMock.Verify(ds => ds.GoOfflineAsync(streamMessage));
		StreamMessage? streamMessageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.False(streamMessageAfter?.IsLive);
		Assert.True(streamMessageAfter?.IsLingering);
	}

	[Fact]
	public async Task HandleExistingStreamsAsync_StreamIsOfflineOnTwitch_DiscordMessageIsOfflineAndLingering_DoesNothing()
	{
		const string streamId = "SomeId";
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		StreamMessage streamMessageBefore = new() { Id = 1, StreamId = streamId, IsLive = false, LingeringSinceUtc = utcNow };

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessageBefore);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = Array.Empty<Stream>() };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock.Object);

		await repository.HandleExistingStreamsAsync();

		Assert.Single(appDbContext.StreamMessages.ToList());
		StreamMessage? streamMessageAfter = await appDbContext.StreamMessages.FindAsync(1);
		Assert.Equal(streamMessageBefore, streamMessageAfter);
		_discordServiceMock.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task HandleExistingStreamsAsync_StreamIsOfflineOnTwitch_DiscordMessageIsNotLiveOrLingering_RemovesStreamMessage()
	{
		const string streamId = "SomeId";
		StreamMessage streamMessageBefore = new() { Id = It.IsAny<int>(), StreamId = streamId, IsLive = false, LingeringSinceUtc = null};

		await using AppDbContext appDbContext = new(_dbContextOptions);
		appDbContext.Add(streamMessageBefore);
		await appDbContext.SaveChangesAsync();

		StreamProvider streamProvider = new() { Streams = Array.Empty<Stream>() };
		SwarmerRepository repository = new(appDbContext, streamProvider, _discordServiceMock.Object);

		await repository.HandleExistingStreamsAsync();

		Assert.Empty(appDbContext.StreamMessages.ToList());
	}

	[Fact]
	public async Task InsertStreamMessageAsync_InsertsStreamMessage()
	{
		await using AppDbContext appDbContext = new(_dbContextOptions);

		SwarmerRepository repository = new(appDbContext, new(), _discordServiceMock.Object);

		StreamMessage streamMessage = new() { Id = It.IsAny<int>(), StreamId = "SomeId" };
		await repository.InsertStreamMessageAsync(streamMessage);

		Assert.Single(appDbContext.StreamMessages.ToList());
	}

	[Fact]
	public async Task RemoveStreamMessageAsync_RemovesStreamMessage()
	{
		await using AppDbContext appDbContext = new(_dbContextOptions);

		StreamMessage streamMessage = new() { Id = It.IsAny<int>(), StreamId = "SomeId" };
		appDbContext.Add(streamMessage);
		await appDbContext.SaveChangesAsync();

		SwarmerRepository repository = new(appDbContext, new(), _discordServiceMock.Object);
		await repository.RemoveStreamMessageAsync(streamMessage);

		Assert.Empty(appDbContext.StreamMessages.ToList());
	}
}
