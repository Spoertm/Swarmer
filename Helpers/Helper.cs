using Discord;
using Newtonsoft.Json;
using Swarmer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Swarmer.Helpers
{
	public class Helper
	{
		private const string _activeStreamsFilePath = "Models/ActiveStreams.json";
		private readonly TwitchAPI _api;
		private readonly Regex _exceptionRegex = new("(?<=   )at.+\n?", RegexOptions.Compiled);

		public Helper(TwitchAPI api)
		{
			_api = api;
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
				.WithColor(1)
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
				.WithDescription("⚫ Offline| " + oldEmbed.Description[9..])
				.WithThumbnailUrl(GetProperUrl(twitchUser.OfflineImageUrl))
				.WithAuthor(oldEmbed.Author!.Value.Name, oldEmbed.Author.Value.IconUrl, oldEmbed.Author.Value.Url)
				.WithColor(1)
				.Build();
		}

		public static List<ActiveStream> DeserializeActiveStreams()
		{
			if (!File.Exists(_activeStreamsFilePath))
				return new();

			string fileConent = File.ReadAllText(_activeStreamsFilePath);
			if (string.IsNullOrWhiteSpace(fileConent))
				return new();

			try
			{
				return JsonConvert.DeserializeObject<List<ActiveStream>>(fileConent);
			}
			catch
			{
				return new();
			}
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

		public static void SerializeActiveStreams(List<ActiveStream> activeStreams)
		{
			File.WriteAllText(_activeStreamsFilePath, JsonConvert.SerializeObject(activeStreams, Formatting.Indented));
		}

		private static string GetProperUrl(string url)
			=> url.Replace("{height}", "1080").Replace("{width}", "1920");
	}
}
