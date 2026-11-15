using DeliveryQuotes.App;
using Xunit;

namespace DeliveryQuotes.Tests;

public class WorkflowTests
{
    // ---------- helpers ----------

    // Takes the value out of a Success. The test fails if the result is a Failure.
    private static T Value<T>(Result<T> result) =>
        Assert.IsType<Result<T>.Success>(result).Value;

    // Takes the error list out of a Failure. The test fails if the result is a Success.
    private static IReadOnlyList<string> Errors<T>(Result<T> result) =>
        Assert.IsType<Result<T>.Failure>(result).Errors;

    // ---------- FACTORY SUCCESS ----------
    [Fact]
    public void Factory_ValidInput_CreatesValidatedValue()
    {
        var result = ValidatedRequest.Create(new RawRequest(" r001 ", "10"));

        var request = Value(result);
        Assert.Equal("R001", request.Id);
        Assert.Equal(10, request.DistanceKm);
    }

    // ---------- FACTORY REJECTION ----------
    [Fact]
    public void Factory_InvalidInput_IsRejectedWithReason()
    {
        Assert.Contains("missing", Errors(ValidatedRequest.Create(null))[0]);
        Assert.Contains("Id is missing", Errors(ValidatedRequest.Create(new RawRequest(null, "5")))[0]);
        Assert.Contains("distance is missing", Errors(ValidatedRequest.Create(new RawRequest("R007", null)))[0]);
        Assert.Contains("not a number", Errors(ValidatedRequest.Create(new RawRequest("R006", "abc")))[0]);
        Assert.Contains("out of range", Errors(ValidatedRequest.Create(new RawRequest("R005", "-3")))[0]);
    }

    // ---------- BOUNDARY VALUE ----------
    [Theory]
    [InlineData("1", true)]
    [InlineData("50", true)]
    [InlineData("0", false)]
    [InlineData("51", false)]
    public void Factory_Boundaries_FollowInclusionRule(string distance, bool expectedValid)
    {
        var result = ValidatedRequest.Create(new RawRequest("R001", distance));

        Assert.Equal(expectedValid, result is Result<ValidatedRequest>.Success);
    }

    // ---------- DISTINCT CASES ----------
    [Fact]
    public void DistinctCases_PatternMatchingDescribesEveryCase()
    {
        var requested = Value(QuoteWorkflow.Start(new RawRequest("R001", "10")));
        var quoted = Value(QuoteWorkflow.Calculate(requested, PricePolicies.Standard));
        var confirmed = Value(QuoteWorkflow.Confirm(quoted));
        var failure = QuoteWorkflow.Start(new RawRequest("R001", "99"));

        Assert.IsType<QuoteState.Requested>(requested);
        Assert.IsType<QuoteState.Quoted>(quoted);
        Assert.IsType<QuoteState.Confirmed>(confirmed);

        Assert.Equal("R001: requested, 10 km", QuoteWorkflow.Describe(QuoteWorkflow.Start(new RawRequest("R001", "10"))));
        Assert.Equal("R001: quoted, price 1500", QuoteWorkflow.Describe(Result<QuoteState>.Ok(quoted)));
        Assert.Equal("R001: CONFIRMED, price 1500", QuoteWorkflow.Describe(Result<QuoteState>.Ok(confirmed)));
        Assert.StartsWith("REJECTED:", QuoteWorkflow.Describe(failure));
    }

    // ---------- NORMAL OPERATION ----------
    [Fact]
    public void NormalOperation_ValidatedValueProducesConfirmedState()
    {
        var result = QuoteWorkflow.Run(new RawRequest("R002", "10"), PricePolicies.Standard);

        Assert.Equal(new QuoteState.Confirmed("R002", 1500), Value(result));
    }

    // ---------- MISSING PREREQUISITE ----------
    [Fact]
    public void MissingPrerequisite_ConfirmWithoutPrice_IsRejected()
    {
        var requested = Value(QuoteWorkflow.Start(new RawRequest("R001", "10")));

        var result = QuoteWorkflow.Confirm(requested);

        Assert.Contains("price has not been calculated", Errors(result)[0]);
    }

    // ---------- REPEATED OPERATION ----------
    [Fact]
    public void RepeatedOperation_SecondConfirmReturnsSameConfirmedState()
    {
        var first = Value(QuoteWorkflow.Run(new RawRequest("R001", "10"), PricePolicies.Standard));

        var second = Value(QuoteWorkflow.Confirm(first));

        Assert.Equal(first, second);
    }

    // ---------- STATE PRESERVATION ----------
    [Fact]
    public void StatePreservation_FailedTransitionLeavesOriginalStateUnchanged()
    {
        var requested = Value(QuoteWorkflow.Start(new RawRequest("R001", "10")));
        var requestedBefore = requested with { };

        var failedConfirm = QuoteWorkflow.Confirm(requested);

        Assert.IsType<Result<QuoteState>.Failure>(failedConfirm);
        Assert.Equal(requestedBefore, requested);

        var quoted = Value(QuoteWorkflow.Calculate(requested, PricePolicies.Standard));
        var quotedBefore = quoted with { };

        var failedCalculate = QuoteWorkflow.Calculate(quoted, PricePolicies.Express);

        Assert.IsType<Result<QuoteState>.Failure>(failedCalculate);
        Assert.Equal(quotedBefore, quoted);
    }

    // ---------- Map and Bind ----------
    [Fact]
    public void Map_Success_TransformsValue()
    {
        var result = Result<int>.Ok(5).Map(x => x * 2);

        Assert.Equal(10, Value(result));
    }

    [Fact]
    public void MapAndBind_AfterFailure_SkipDependentSteps()
    {
        bool mapCalled = false;
        bool bindCalled = false;

        var failed = Result<int>.Fail("first step failed");

        var mapped = failed.Map(x => { mapCalled = true; return x * 2; });
        var bound = failed.Bind(x => { bindCalled = true; return Result<int>.Ok(x); });

        Assert.False(mapCalled);
        Assert.False(bindCalled);
        Assert.Equal("first step failed", Errors(mapped)[0]);
        Assert.Equal("first step failed", Errors(bound)[0]);
    }

    [Fact]
    public void Run_InvalidInput_NeverCallsPricePolicy()
    {
        bool policyCalled = false;

        var result = QuoteWorkflow.Run(new RawRequest("R005", "-3"), distance =>
        {
            policyCalled = true;
            return distance;
        });

        Assert.False(policyCalled);
        Assert.IsType<Result<QuoteState>.Failure>(result);
    }
}