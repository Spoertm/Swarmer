namespace Swarmer.Domain.Services;

public class KeepDynoAliveService : AbstractBackgroundService
{
	private readonly IHttpClientFactory _httpClientFactory;

	public KeepDynoAliveService(IHttpClientFactory httpClientFactory)
	{
		_httpClientFactory = httpClientFactory;
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		const string url = "https://swarmer.herokuapp.com/";
		await _httpClientFactory.CreateClient().GetStringAsync(url, stoppingToken);
	}
}
