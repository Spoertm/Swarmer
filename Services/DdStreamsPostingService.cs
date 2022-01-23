global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Swarmer.Services;

public class DdStreamsPostingService : AbstractBackgroundService
{
	public DdStreamsPostingService()
	{
	}

	protected override TimeSpan Interval => TimeSpan.FromSeconds(20);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
	}
}
