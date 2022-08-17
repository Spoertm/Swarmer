using Serilog;
using TwitchLib.Api;
using TwitchLib.Api.Auth;

namespace Swarmer.Services;

public class AccessTokenNotifierService : AbstractBackgroundService
{
	private readonly IConfiguration _config;
	private readonly TwitchAPI _twitchApi;

	public AccessTokenNotifierService(IConfiguration config, TwitchAPI twitchApi)
	{
		_config = config;
		_twitchApi = twitchApi;
	}

	protected override TimeSpan Interval => TimeSpan.FromDays(1);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		ValidateAccessTokenResponse? response = await _twitchApi.Auth.ValidateAccessTokenAsync(_config["AccessToken"]);
		if (response is null)
		{
			Log.Fatal("The access token is no longer valid and has to be renewed");
			return;
		}

		const int timeLimitDays = 10;
		TimeSpan timeLeft = TimeSpan.FromSeconds(Convert.ToDouble(response.ExpiresIn));
		if (timeLeft.Days < timeLimitDays)
			Log.Fatal("It's {DaysLeft} days left before the access token expires", timeLeft.Days);
	}
}
