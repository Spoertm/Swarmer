using Discord;
using Swarmer.Models.Enums;
using System;
using System.Text.RegularExpressions;

namespace Swarmer.Helpers
{
	public static class EmbedHelper
	{
		private static readonly Regex _exceptionRegex = new("(?<=   )at.+\n?", RegexOptions.Compiled);

		public static Embed GetOnlineStreamEmbed(
			string title,
			string username,
			string thumbnailUrl,
			string avatarUrl,
			string streamUrl,
			StreamingPlatform platform)
		{
			uint accentColor = platform switch
			{
				StreamingPlatform.Twitch  => 6570404,
				StreamingPlatform.YouTube => 16711680,
				_                         => throw new ArgumentException($"Unsupported platform: {platform}"),
			};

			return new EmbedBuilder()
				.WithDescription("🔴 Live| " + title)
				.WithThumbnailUrl(thumbnailUrl)
				.WithAuthor(username, avatarUrl, streamUrl)
				.WithColor(accentColor)
				.Build();
		}

		public static Embed GetOfflineEmbed(IEmbed oldEmbed, string? newThumbnailUrl = null)
		{
			return new EmbedBuilder()
				.WithDescription("⚫ Offline| " + (oldEmbed.Description.Length >= 9 ? oldEmbed.Description[9..] : string.Empty))
				.WithThumbnailUrl(newThumbnailUrl ?? oldEmbed.Thumbnail!.Value.Url)
				.WithAuthor(oldEmbed.Author!.Value.Name, oldEmbed.Author.Value.IconUrl, oldEmbed.Author.Value.Url)
				.WithColor(1)
				.Build();
		}

		public static Embed ExceptionEmbed(LogMessage msg)
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

		private static void FillExceptionEmbedBuilder(Exception? exception, EmbedBuilder exceptionEmbed)
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
	}
}
