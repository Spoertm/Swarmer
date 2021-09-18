using OpenQA.Selenium;
using Swarmer.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Swarmer.Tools
{
	public class YoutubeStreamsFetcher
	{
		private readonly IWebDriver _driver;

		public YoutubeStreamsFetcher(IWebDriver driver)
		{
			_driver = driver;
			_driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(5);
			_driver.Url = "https://www.youtube.com/results?search_query=%22devil+daggers%22&sp=EgJAAQ%253D%253D";
		}

		public YoutubeStream[] GetStreams()
		{
			_driver.Navigate().Refresh();
			if (_driver.PageSource.Contains("No results found"))
				return Array.Empty<YoutubeStream>();

			((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollBy(0,900)");
			ReadOnlyCollection<IWebElement> channelInfos = _driver.FindElements(By.XPath("//*[@id=\"dismissible\"]"));
			string[] channelSummaries = channelInfos.Select(e => e.Text).ToArray();
			string[] titles = channelSummaries.Select(cs => cs.Split(Environment.NewLine)[0]).ToArray();
			string[] usernames = channelInfos.Select(ci => ci.FindElement(By.XPath(".//*[@id='text-container']/*[@id='text']/a")).GetAttribute("text")).ToArray();
			string[] streamUrls = channelInfos.Select(e => e.FindElement(By.Id("thumbnail")).GetAttribute("href")).ToArray();
			string[] avatarUrls = channelInfos.Select(e => e.FindElement(By.Id("channel-info")).FindElement(By.Id("img")).GetAttribute("src")).ToArray();
			string[] thumbnailUrls = channelInfos.Select(e => e.FindElement(By.Id("img")).GetAttribute("src")).ToArray();

			string[][] resultCollections = { titles, usernames, streamUrls, avatarUrls, thumbnailUrls };
			if (resultCollections.Any(rc => rc.Length != titles.Length))
				throw new("Not equally large collections.");

			List<YoutubeStream> streams = new();
			for (int i = 0; i < titles.Length; i++)
			{
				streams.Add(new(
					titles[i],
					usernames[i],
					streamUrls[i],
					avatarUrls[i],
					thumbnailUrls[i]));
			}

			return streams.ToArray();
		}
	}
}
