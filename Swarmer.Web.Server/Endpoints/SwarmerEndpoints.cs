using Microsoft.AspNetCore.Mvc;
using Swarmer.Domain.Models;
using Swashbuckle.AspNetCore.Annotations;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Swarmer.Web.Server.Endpoints;

public static class SwarmerEndpoints
{
	private const string _ddName = "devil daggers";
	private const string _hdName = "hyper demon";

	public static void RegisterSwarmerEndpoints(this WebApplication app)
	{
		app.MapGet("streams", DdTwitchStreams).WithTags("Streams");
	}

	[SwaggerOperation(description: @"Returns streams for all games if nothing is specified;
otherwise the streams for a specific game (can only be ""devil daggers"" or ""hyper demon"").")]
	private static Stream[]? DdTwitchStreams([FromServices] StreamProvider provider, string? gameName = null)
	{
		if (provider.Streams is null || gameName is null)
		{
			return provider.Streams;
		}

		return gameName.ToLower() switch
		{
			_ddName => Array.FindAll(provider.Streams, stream => stream.GameName.Equals(_ddName, StringComparison.OrdinalIgnoreCase)),
			_hdName => Array.FindAll(provider.Streams, stream => stream.GameName.Equals(_hdName, StringComparison.OrdinalIgnoreCase)),
			_       => provider.Streams,
		};
	}
}
