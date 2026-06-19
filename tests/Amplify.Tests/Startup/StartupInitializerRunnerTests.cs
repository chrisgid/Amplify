using Amplify.Core.Startup;
using NSubstitute;

namespace Amplify.Tests.Startup;

public class StartupInitializerRunnerTests
{
    [Fact]
    public async Task RunsInitializersInAscendingOrder()
    {
        var calls = new List<int>();

        IStartupInitializer Make(int order)
        {
            IStartupInitializer initializer = Substitute.For<IStartupInitializer>();
            initializer.Order.Returns(order);
            initializer.OnLaunchedAsync(Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    calls.Add(order);
                    return Task.CompletedTask;
                });
            return initializer;
        }

        // Deliberately out of order; the runner must sort by Order.
        IStartupInitializer[] initializers = [Make(400), Make(100), Make(200)];

        await StartupInitializerRunner.RunAsync(initializers, CancellationToken.None);

        Assert.Equal([100, 200, 400], calls);
    }

    [Fact]
    public async Task ThrowsWhenAlreadyCancelled()
    {
        IStartupInitializer initializer = Substitute.For<IStartupInitializer>();
        initializer.Order.Returns(100);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => StartupInitializerRunner.RunAsync([initializer], cts.Token));

        await initializer.DidNotReceive().OnLaunchedAsync(Arg.Any<CancellationToken>());
    }
}
