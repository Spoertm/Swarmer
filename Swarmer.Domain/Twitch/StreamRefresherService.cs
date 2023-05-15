using Swarmer.Domain.Models;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Domain.Twitch;

public sealed class StreamRefresherService : AbstractBackgroundService
{
	private readonly ITwitchAPI _twitchApi;
	private readonly StreamProvider _streamProvider;
	private static readonly List<string> _twitchGameIds = new()
	{
		"490905", // Devil Daggers
		"1350012934", // HYPER DEMON
	};

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
			GetStreamsResponse streamResponse = await _twitchApi.Helix.Streams.GetStreamsAsync(first: 100, gameIds: _twitchGameIds);
			Stream[] twitchStreams = streamResponse.Streams;
			_streamProvider.Streams = twitchStreams;
		}
	}
}
