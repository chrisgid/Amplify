using Amplify.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Amplify.Tests.Configuration;

public class SpotifyOptionsBindingTests
{
    [Fact]
    public void BindsRedirectPortAndScopesFromConfigurationSection()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Spotify:RedirectPort"] = "49737",
                ["Spotify:Scopes:0"] = "user-read-playback-state",
                ["Spotify:Scopes:1"] = "user-modify-playback-state",
            })
            .Build();

        SpotifyOptions options = config.GetSection(SpotifyOptions.SectionName).Get<SpotifyOptions>()!;

        Assert.Equal(49737, options.RedirectPort);
        Assert.Equal(["user-read-playback-state", "user-modify-playback-state"], options.Scopes);
    }
}
