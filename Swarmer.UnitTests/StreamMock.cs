using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace Swarmer.UnitTests;

public class MockStream : Stream
{
	public MockStream(
		string id,
		string userId = null!,
		string userLogin = null!,
		string userName = null!,
		string gameId = null!,
		string gameName = null!)
	{
		Id = id;
		UserId = userId;
		UserLogin = userLogin;
		UserName = userName;
		GameId = gameId;
		GameName = gameName;
	}
}
