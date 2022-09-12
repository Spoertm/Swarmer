using Swarmer.Domain.Models;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace Swarmer.Domain.Services;

public sealed class StreamRefresherService : AbstractBackgroundService
{
	private readonly List<string> _twitchGameIds = new()
	{
		"490905", // Devil Daggers
	};
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
		if (!stoppingToken.IsCancellationRequested)
		{
			GetStreamsResponse streamResponse = await _twitchApi.Helix.Streams.GetStreamsAsync(first: 50, gameIds: _twitchGameIds);
			Stream[] twitchStreams = streamResponse.Streams;
			_streamProvider.Streams = twitchStreams;
		}
	}
}
