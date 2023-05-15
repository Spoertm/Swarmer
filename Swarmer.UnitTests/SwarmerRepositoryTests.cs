using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Swarmer.Domain.Models;
using Swarmer.Domain.Models.Database;
using Swarmer.Domain.Services;
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
	private readonly DiscordService _discordService;

	public SwarmerRepositoryTests()
	{
		Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

		_dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;

		IConfiguration configMock = Mock.Of<IConfiguration>();
		Mock<SwarmerDiscordClient> discordClientMock = new(() => new(configMock, new()));
		Mock<DiscordService> discordServiceMock = new(() => new(discordClientMock.Object));
		_discordService = discordServiceMock.Object;
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

		SwarmerRepository sut = new(appDbContext, streamProvider, _discordService);
		List<StreamToPost> result = sut.GetStreamsToPost().ToList();

		Assert.Single(result);
		Assert.Equal(stream2, result[0].Stream);
		Assert.Equal(gameChannel1, result[0].Channel);
	}

	[Fact]
	public async Task UpdateLingeringStreamMessages_UpdatesLingeringStreams()
	{
		await using AppDbContext appDbContext = new(_dbContextOptions);

		SwarmerRepository sut = new(appDbContext, new(), _discordService);

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
}
