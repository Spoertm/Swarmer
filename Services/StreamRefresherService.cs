using Swarmer.Models;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace Swarmer.Services;

public class StreamRefresherService : AbstractBackgroundService
{
	private const string _devilDaggersId = "490905";
	private readonly TwitchAPI _twitchApi;
	private readonly StreamProvider _streamProvider;

	public StreamRefresherService(TwitchAPI twitchApi, StreamProvider streamProvider)
	{
		_twitchApi = twitchApi;
		_streamProvider = streamProvider;
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		GetStreamsResponse streamResponse = await _twitchApi.Helix.Streams.GetStreamsAsync(first: 50, gameIds: new() { _devilDaggersId });
		Stream[] twitchStreams = streamResponse.Streams;
		_streamProvider.Streams = twitchStreams;
	}
}
