using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Swarmer.Models;
using Swarmer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Swarmer.Helpers
{
	public class Helper
	{
		private readonly string _activeStreamsFilePath = Path.Combine(AppContext.BaseDirectory, "Models", "ActiveStreams.json");
		private readonly TwitchAPI _api;
		private readonly SocketTextChannel _swarmerActiveStreamsChannel;
		private readonly LoggingService _loggingService;
		private readonly Regex _exceptionRegex = new("(?<=   )at.+\n?", RegexOptions.Compiled);

		public Helper(TwitchAPI api, Config config, DiscordSocketClient client, LoggingService loggingService)
		{
			_api = api;
			_swarmerActiveStreamsChannel = (client.GetChannel(config.SwarmerActiveStreamsChannelId) as SocketTextChannel)!;
			_loggingService = loggingService;
		}

		public async Task<Embed> GetOnlineStreamEmbedAsync(Stream twitchStream)
		{
			User twitchUser = (await _api.Helix.Users.GetUsersAsync(ids: new()
				{
					twitchStream.UserId,
				}))
				.Users[0];

			string iconUrl = GetProperUrl(twitchUser.ProfileImageUrl);
			return new EmbedBuilder()
				.WithDescription("🔴 Live| " + twitchStream.Title)
				.WithThumbnailUrl(GetProperUrl(twitchStream.ThumbnailUrl))
				.WithAuthor(twitchStream.UserName, iconUrl, "https://twitch.tv/" + twitchStream.UserName)
				.WithColor(6570404)
				.Build();
		}

		public async Task<Embed> GetOfflineEmbedAsync(IEmbed oldEmbed, string userId)
		{
			User twitchUser = (await _api.Helix.Users.GetUsersAsync(ids: new()
				{
					userId,
				}))
				.Users[0];

			return new EmbedBuilder()
				.WithDescription("⚫ Offline| " + (oldEmbed.Description.Length >= 9 ? oldEmbed.Description[9..] : string.Empty))
				.WithThumbnailUrl(GetProperUrl(twitchUser.OfflineImageUrl))
				.WithAuthor(oldEmbed.Author!.Value.Name, oldEmbed.Author.Value.IconUrl, oldEmbed.Author.Value.Url)
				.WithColor(1)
				.Build();
		}

		public async Task<List<ActiveStream>> DeserializeActiveStreams()
		{
			IAttachment? latestAttachment = (await _swarmerActiveStreamsChannel.GetMessagesAsync(1).FlattenAsync())
				.FirstOrDefault()?
				.Attachments
				.FirstOrDefault();

			if (latestAttachment is null)
			{
				await _loggingService.LogAsync(new(LogSeverity.Error, "DeserializerActiveStreams()", "File in the latest message in ActiveStreams channel is null."));
				throw new("File in the latest message in ActiveStreams channel is null.");
			}

			using HttpClient httpClient = new();
			string activeStreamsJson = await httpClient.GetStringAsync(latestAttachment.Url);
			try
			{
				await File.WriteAllTextAsync(_activeStreamsFilePath, activeStreamsJson);
				List<ActiveStream>? activeStreams = JsonConvert.DeserializeObject<List<ActiveStream>>(activeStreamsJson);
				return activeStreams ?? new();
			}
			catch
			{
				return new();
			}
		}

		public async Task SerializeAndUpdateActiveStreams(List<ActiveStream> activeStreams)
		{
			await File.WriteAllTextAsync(_activeStreamsFilePath, JsonConvert.SerializeObject(activeStreams, Formatting.Indented));
			await _swarmerActiveStreamsChannel.SendFileAsync(_activeStreamsFilePath, string.Empty);
		}

		public Embed ExceptionEmbed(LogMessage msg)
		{
			EmbedBuilder exceptionEmbed = new EmbedBuilder()
				.WithTitle(msg.Exception?.GetType().Name ?? "Exception thrown")
				.AddField("Severity", msg.Severity, true)
				.AddField("Source", msg.Source, true)
				.WithCurrentTimestamp();

			Exception? ex = msg.Exception;

			if (ex is null)
				exceptionEmbed.AddField("Message", msg.Message);

			FillExceptionEmbedBuilder(ex, exceptionEmbed);

			return exceptionEmbed.Build();
		}

		private void FillExceptionEmbedBuilder(Exception? exception, EmbedBuilder exceptionEmbed)
		{
			string? exString = exception?.ToString();
			if (exString is not null)
			{
				Match regexMatch = _exceptionRegex.Match(exString);
				exceptionEmbed.AddField("Location", regexMatch.Value);
			}

			while (exception is not null)
			{
				exceptionEmbed.AddField(exception.GetType().Name, string.IsNullOrEmpty(exception.Message) ? "No message." : exception.Message);
				exception = exception.InnerException;
			}
		}

		private static string GetProperUrl(string url)
			=> url.Replace("{height}", "1080").Replace("{width}", "1920");
	}
}
