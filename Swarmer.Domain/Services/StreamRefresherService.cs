using Swarmer.Domain.Models;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Domain.Services;

public class StreamRefresherService : AbstractBackgroundService
{
	private const string _devilDaggersId = "490905";
	private readonly ITwitchAPI _twitchApi;
	private readonly StreamProvider _streamProvider;

	public StreamRefresherService(ITwitchAPI twitchApi, StreamProvider streamProvider)
	{
		_twitchApi = twitchApi;
		_streamProvider = streamProvider;
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		if (!stoppingToken.IsCancellationRequested)
		{
			GetStreamsResponse streamResponse = await _twitchApi.Helix.Streams.GetStreamsAsync(first: 50, gameIds: new() { _devilDaggersId });
			Stream[] twitchStreams = streamResponse.Streams;
			_streamProvider.Streams = twitchStreams;
		}
	}
}
