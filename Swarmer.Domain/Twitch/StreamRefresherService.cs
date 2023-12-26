using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Swarmer.Domain.Database;
using Swarmer.Domain.Models;
using System.Text.Json;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Domain.Twitch;

public sealed class StreamRefresherService : RepeatingBackgroundService
{
	private static readonly List<string> _twitchGameIds =
	[
		"490905", // Devil Daggers
		"1350012934", // HYPER DEMON
	];
	private readonly ITwitchAPI _twitchApi;
	private readonly StreamProvider _streamProvider;
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly SwarmerConfig _config;

	public StreamRefresherService(
		ITwitchAPI twitchApi,
		StreamProvider streamProvider,
		IOptions<SwarmerConfig> options,
		IServiceScopeFactory serviceScopeFactory)
	{
		_twitchApi = twitchApi;
		_streamProvider = streamProvider;
		_config = options.Value;
		_serviceScopeFactory = serviceScopeFactory;
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		if (stoppingToken.IsCancellationRequested)
		{
			return;
		}

		try
		{
			GetStreamsResponse streamResponse = await _twitchApi.Helix.Streams.GetStreamsAsync(first: 100, gameIds: _twitchGameIds);
			Stream[] twitchStreams = streamResponse.Streams;
			_streamProvider.Streams = twitchStreams;
		}
		catch (BadScopeException ex)
		{
			Log.Warning(ex, "Twitch API request failed due to an expired access token. Refreshing token...");
			RefreshTokenResponse tokenRefreshResponse = await RequestTokenAsync();

			await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
			ConfigRepository repo = scope.ServiceProvider.GetRequiredService<ConfigRepository>();
			await repo.UpdateAccessToken(tokenRefreshResponse.AccessToken);

			_twitchApi.Settings.AccessToken = tokenRefreshResponse.AccessToken;

			Log.Information(
				"Updated Twitch access token. The new token is valid for {ExpirationSeconds} seconds, expiring on {ExpirationDate} UTC",
				tokenRefreshResponse.ExpiresIn,
				DateTimeOffset.UtcNow.AddSeconds(tokenRefreshResponse.ExpiresIn).ToString("dd/MM/yyyy")
			);
		}
	}

	private async Task<RefreshTokenResponse> RequestTokenAsync()
	{
		const string reqUrl = "https://id.twitch.tv/oauth2/token";
		using HttpClient client = new();
		Dictionary<string, string> postValues = new()
		{
			{ "client_id", _config.ClientId },
			{ "client_secret", _config.ClientSecret },
			{ "grant_type", "client_credentials" },
		};

		HttpResponseMessage response = await client.PostAsync(reqUrl, new FormUrlEncodedContent(postValues));
		response.EnsureSuccessStatusCode();

		System.IO.Stream stream = await response.Content.ReadAsStreamAsync();
		RefreshTokenResponse refreshTokenResponse = await JsonSerializer.DeserializeAsync<RefreshTokenResponse>(stream)
													?? throw new("Access token deserialization resulted in null.");

		return refreshTokenResponse;
	}
}
