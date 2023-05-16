using Microsoft.AspNetCore.Mvc;
using Swarmer.Domain.Twitch;
using Swashbuckle.AspNetCore.Annotations;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Swarmer.Web.Server.Endpoints;

public static class SwarmerEndpoints
{
	public const string DdEndpointParamName = "devil daggers";
	public const string HdEndpointParamName = "hyper demon";

	public static void RegisterSwarmerEndpoints(this WebApplication app)
	{
		app.MapGet("streams", DdTwitchStreams).WithTags("Streams");
	}

	[SwaggerOperation(description:
		$"""
		Returns streams for all games if nothing is specified;
		otherwise the streams for a specific game (can only be "{DdEndpointParamName}" or "{HdEndpointParamName}").
		"""
	)]
	public static Stream[]? DdTwitchStreams([FromServices] StreamProvider provider, string? gameName = null)
	{
		if (provider.Streams is null || gameName is null)
		{
			return provider.Streams;
		}

		return gameName.ToLower() switch
		{
			DdEndpointParamName => Array.FindAll(provider.Streams, stream => stream.GameName.Equals(DdEndpointParamName, StringComparison.OrdinalIgnoreCase)),
			HdEndpointParamName => Array.FindAll(provider.Streams, stream => stream.GameName.Equals(HdEndpointParamName, StringComparison.OrdinalIgnoreCase)),
			_                   => provider.Streams,
		};
	}
}
