namespace Pigeon.Testing.Tests;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using System.Reflection;

public class PigeonTestingScannerValidationTests
{
    [Fact]
    public void Constructor_RequiresServices()
        => Assert.Throws<ArgumentNullException>(() => new PigeonTestingConsumerScanner(null, Substitute.For<IConsumingConfigurator>()));

    [Fact]
    public void Constructor_RequiresConfigurator()
        => Assert.Throws<ArgumentNullException>(() => new PigeonTestingConsumerScanner(new ServiceCollection(), null));

    [Fact]
    public void Scan_NullAssembliesDoesNothing()
        => new PigeonTestingConsumerScanner(new ServiceCollection(), Substitute.For<IConsumingConfigurator>()).Scan(null);

    [Fact]
    public void RegisterMethod_RejectsNonTaskReturn()
    {
        var error = Assert.Throws<TargetInvocationException>(() => Register(typeof(string).GetMethod(nameof(ToString), Type.EmptyTypes)!));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public void RegisterMethod_RequiresMessageParameter()
    {
        var method = typeof(PigeonTestingScannerValidationTests).GetMethod(nameof(OnlyCancellation), BindingFlags.Instance | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => Register(method));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private Task OnlyCancellation(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void Register(MethodInfo method)
    {
        var scanner = new PigeonTestingConsumerScanner(new ServiceCollection(), Substitute.For<IConsumingConfigurator>());
        typeof(PigeonTestingConsumerScanner).GetMethod("RegisterConsumerMethod", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(scanner, new object[] { typeof(PigeonTestingScannerValidationTests), method });
    }
}
