namespace Pigeon.Messaging.Tests.Consuming.Dispatching;

using Pigeon.Messaging.Consuming.Dispatching;

public class PigeonConsumeOutcomeTests
{
    [Fact]
    public void Failure_PreservesStructuredDetails()
    {
        var failure = new PigeonConsumeFailure { Code = "invalid", Type = "validation", Message = "Invalid order", IsRetryable = false };
        Assert.Equal("invalid", failure.Code);
        Assert.Equal("validation", failure.Type);
        Assert.Equal("Invalid order", failure.Message);
        Assert.False(failure.IsRetryable);
    }

    [Fact]
    public void Outcome_PreservesFailure()
    {
        var failure = new PigeonConsumeFailure { Code = "failed" };
        var outcome = new PigeonConsumeOutcome { Failure = failure };
        Assert.Same(failure, outcome.Failure);
    }

    [Fact]
    public void Failure_CopyPreservesValues()
    {
        var original = new PigeonConsumeFailure { Code = "invalid", Message = "failed" };
        var copy = original with { };
        Assert.Equal(original, copy);
        Assert.NotSame(original, copy);
    }

    [Fact]
    public void Outcome_CopyPreservesValues()
    {
        var original = new PigeonConsumeOutcome { Failure = new PigeonConsumeFailure { Code = "invalid" } };
        var copy = original with { };
        Assert.Equal(original, copy);
        Assert.NotSame(original, copy);
    }
}
