using Swarmer.Domain.Database;

namespace Swarmer.Domain.Twitch;

public record struct StreamToPost(Stream Stream, GameChannel Channel);
