using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Swarmer.Services
{
	public abstract class AbstractBackgroundService : BackgroundService
	{
		protected abstract TimeSpan Interval { get; }

		protected abstract Task ExecuteTaskAsync(CancellationToken stoppingToken);

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			Console.WriteLine("In ExecuteTaskAsync...");
			while (!stoppingToken.IsCancellationRequested)
			{
				await ExecuteTaskAsync(stoppingToken);

				if (Interval.TotalMilliseconds > 0)
					await Task.Delay(Interval, stoppingToken);
			}

			throw new("Background service stopped.");
		}
	}
}
