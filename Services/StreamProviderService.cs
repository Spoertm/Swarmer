using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace Swarmer.Services;

public class StreamProviderService : AbstractBackgroundService
{
	private readonly string _devilDaggersId;
	private readonly TwitchAPI _twitchApi;
	public Stream[]? Streams { get; private set; }

	public StreamProviderService(IConfiguration config, TwitchAPI twitchApi)
	{
		_devilDaggersId = config["DdTwitchGameId"];
		_twitchApi = twitchApi;
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		GetStreamsResponse streamResponse = await _twitchApi.Helix.Streams.GetStreamsAsync(first: 50, gameIds: new() { _devilDaggersId });
		Stream[] twitchStreams = streamResponse.Streams;
		Streams = twitchStreams;
	}
}
