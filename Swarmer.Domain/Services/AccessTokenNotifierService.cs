using Microsoft.Extensions.Configuration;
using Serilog;
using TwitchLib.Api;
using TwitchLib.Api.Auth;

namespace Swarmer.Domain.Services;

public sealed class AccessTokenNotifierService : AbstractBackgroundService
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
		if (stoppingToken.IsCancellationRequested)
		{
			return;
		}

		ValidateAccessTokenResponse? response = await _twitchApi.Auth.ValidateAccessTokenAsync(_config["AccessToken"]);
		if (response is null)
		{
			Log.Error("The access token is no longer valid and has to be renewed");
			return;
		}

		const int timeLimitDays = 10;
		TimeSpan timeLeft = TimeSpan.FromSeconds(response.ExpiresIn);
		if (timeLeft.Days < timeLimitDays)
		{
			Log.Warning("It's {DaysLeft} days left before the access token expires", timeLeft.Days);
		}
	}
}
