using Swarmer.Models;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace Swarmer.Services;

public class UpdateStreamsCacheService : AbstractBackgroundService
{
	private readonly string _devilDaggersId;
	private readonly TwitchAPI _twitchApi;
	private readonly DdStreamProvider _ddStreamProvider;

	public UpdateStreamsCacheService(
		TwitchAPI twitchApi,
		DdStreamProvider ddStreamProvider)
	{
		_devilDaggersId = Environment.GetEnvironmentVariable("DdTwitchGameId")!;
		_twitchApi = twitchApi;
		_ddStreamProvider = ddStreamProvider;
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		GetStreamsResponse streamResponse = await _twitchApi.Helix.Streams.GetStreamsAsync(first: 50, gameIds: new() { _devilDaggersId });
		Stream[] twitchStreams = streamResponse.Streams;
		_ddStreamProvider.Streams = twitchStreams;
	}
}
