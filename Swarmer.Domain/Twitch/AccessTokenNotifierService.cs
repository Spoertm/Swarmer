using Microsoft.Extensions.Options;
using Serilog;
using Swarmer.Domain.Models;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Domain.Twitch;

public sealed class AccessTokenNotifierService : AbstractBackgroundService
{
	private readonly SwarmerConfig _config;
	private readonly ITwitchAPI _twitchApi;

	public AccessTokenNotifierService(IOptions<SwarmerConfig> options, ITwitchAPI twitchApi)
	{
		_config = options.Value;
		_twitchApi = twitchApi;
	}

	protected override TimeSpan Interval => TimeSpan.FromDays(1);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		if (stoppingToken.IsCancellationRequested)
		{
			return;
		}

		ValidateAccessTokenResponse? response = await _twitchApi.Auth.ValidateAccessTokenAsync(_config.AccessToken);
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
