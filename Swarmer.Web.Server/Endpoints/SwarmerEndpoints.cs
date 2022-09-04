namespace Swarmer.Web.Server.Endpoints;

public static class SwarmerEndpoints
{
	public static void RegisterSwarmerEndpoints(this WebApplication app)
	{
		app.MapGet("streams", DdTwitchStreams).WithTags("Streams");
	}

	private static string DdTwitchStreams()
	{
		return "Streams!";
	}
}
