using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace Swarmer.Tests.UnitTests;

public sealed class MockStream : Stream
{
    public MockStream(
        string id,
        string? userId = null,
        string? userLogin = null,
        string? userName = null,
        string? gameId = null,
        string? gameName = null,
        string? title = null,
        string? thumbnailUrl = null)
    {
        Id = id;
        UserId = userId;
        UserLogin = userLogin;
        UserName = userName;
        GameId = gameId;
        GameName = gameName;
        Title = title;
        ThumbnailUrl = thumbnailUrl ?? "https://static-cdn.jtvnw.net/previews-ttv/live_user_test-{width}x{height}.jpg";
    }
}
