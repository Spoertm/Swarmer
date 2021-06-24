using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Swarmer.Helpers;
using Swarmer.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Swarmer.Services
{
	public class LoggingService
	{
		private readonly SocketTextChannel _swarmerInfoChannel;
		private readonly Helper _helper;

		public LoggingService(DiscordSocketClient client, CommandService commands, Config config, Helper helper)
		{
			_helper = helper;
			LogDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");

			client.Log += LogAsync;
			commands.Log += LogAsync;

			_swarmerInfoChannel = (client.GetChannel(config.SwarmerInfoChannelId) as SocketTextChannel)!;
		}

		private string LogDirectory { get; }
		private string LogFile => Path.Combine(LogDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.txt");

		public async Task LogAsync(LogMessage logMessage)
		{
			Directory.CreateDirectory(LogDirectory);

			string logText = $"{DateTime.Now:hh:mm:ss} [{logMessage.Severity}] {logMessage.Source}: {logMessage.Exception?.ToString() ?? logMessage.Message}";
			await File.AppendAllTextAsync(LogFile, $"{logText}\n\n");

			Embed exceptionEmbed = _helper.ExceptionEmbed(logMessage);
			await _swarmerInfoChannel.SendMessageAsync(embed: exceptionEmbed);
		}
	}
}
