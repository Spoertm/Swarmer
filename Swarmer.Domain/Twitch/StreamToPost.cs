using Swarmer.Domain.Data;

namespace Swarmer.Domain.Twitch;

public record struct StreamToPost(Stream Stream, GameChannel Channel);
