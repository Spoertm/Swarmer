using Swarmer.Domain.Twitch;
using Swarmer.Web.Server.Endpoints;
using System;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using Xunit;

namespace Swarmer.UnitTests;

public class SwarmerEndpointsTests
{
	[Fact]
	public void DdTwitchStreams_NoGameName_ReturnsAllStreams()
	{
		MockStream stream1 = new("1", gameId: "Game 1");
		MockStream stream2 = new("2", gameId: "Game 2");

		StreamProvider streamProvider = new()
		{
			Streams = [stream1, stream2],
		};

		Stream[]? result = SwarmerEndpoints.DdTwitchStreams(streamProvider);

		Assert.Equal(streamProvider.Streams, result);
	}

	[Fact]
	public void DdTwitchStreams_WithGameName_ReturnsFilteredStreams()
	{
		Stream[] streams =
		[
			new MockStream("1", gameName: SwarmerEndpoints.DdEndpointParamName),
			new MockStream("2", gameName: SwarmerEndpoints.DdEndpointParamName),
			new MockStream("3", gameName: SwarmerEndpoints.HdEndpointParamName),
		];

		StreamProvider streamProvider = new()
		{
			Streams = streams,
		};

		Stream[]? ddEndpointResult = SwarmerEndpoints.DdTwitchStreams(streamProvider, SwarmerEndpoints.DdEndpointParamName);
		Stream[]? hdEndpointResult = SwarmerEndpoints.DdTwitchStreams(streamProvider, SwarmerEndpoints.HdEndpointParamName);

		Assert.NotNull(ddEndpointResult);
		Assert.NotNull(hdEndpointResult);

		// ReSharper disable ReturnValueOfPureMethodIsNotUsed
		Assert.All(ddEndpointResult, s => s.GameName.Equals(SwarmerEndpoints.DdEndpointParamName, StringComparison.OrdinalIgnoreCase));
		Assert.All(hdEndpointResult, s => s.GameName.Equals(SwarmerEndpoints.HdEndpointParamName, StringComparison.OrdinalIgnoreCase));
		// ReSharper restore ReturnValueOfPureMethodIsNotUsed
	}
}
