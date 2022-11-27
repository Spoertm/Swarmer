namespace Swarmer.Domain.Services;

public sealed class KeepAppAliveService : AbstractBackgroundService
{
	private readonly IHttpClientFactory _httpClientFactory;

	public KeepAppAliveService(IHttpClientFactory httpClientFactory)
	{
		_httpClientFactory = httpClientFactory;
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

	protected override Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		const string url = "https://swarmerbot.azurewebsites.net/";
		_ = _httpClientFactory.CreateClient().GetStringAsync(url, stoppingToken);
		return Task.CompletedTask;
	}
}
