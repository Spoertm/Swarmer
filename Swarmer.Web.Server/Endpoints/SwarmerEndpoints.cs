using Swarmer.Domain.Models;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Swarmer.Web.Server.Endpoints;

public static class SwarmerEndpoints
{
	public static void RegisterSwarmerEndpoints(this WebApplication app)
	{
		app.MapGet("streams", DdTwitchStreams).WithTags("Streams");
	}

	private static Stream[]? DdTwitchStreams(StreamProvider provider)
	{
		return provider.Streams;
	}
}
