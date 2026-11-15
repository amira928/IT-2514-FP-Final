using DeliveryQuotes.App;
using Xunit;

namespace DeliveryQuotes.Tests;

public class PipelineTests
{
    // Case 1: normal workflow
    [Fact]
    public void NormalWorkflow_ProducesDocumentedResult()
    {
        var requests = new List<DeliveryRequest>
        {
            new("R001", 2),
            new("R002", 10)
        };

        var summary = Pipeline.Run(requests, PricePolicies.Standard);

        Assert.Equal(2, summary.Count);
        Assert.Equal(2200, summary.TotalPrice);
        Assert.Equal("R001", summary.Quotes[0].Id);
        Assert.Equal(700, summary.Quotes[0].Price);
        Assert.Equal(1500, summary.Quotes[1].Price);
    }

    // Case 2: valid empty dataset
    [Fact]
    public void EmptyDataset_ProducesEmptySummary()
    {
        var summary = Pipeline.Run(new List<DeliveryRequest>(), PricePolicies.Standard);

        Assert.Equal(0, summary.Count);
        Assert.Equal(0, summary.TotalPrice);
        Assert.Empty(summary.Quotes);
    }

    // Case 3: boundary values (inclusion rule)
    [Theory]
    [InlineData("1", true)]
    [InlineData("50", true)]
    [InlineData("0", false)]
    [InlineData("51", false)]
    public void BoundaryValues_FollowInclusionRule(string distance, bool expectedValid)
    {
        bool isValid = RequestParser.TryParse(new RawRequest("R001", distance), out _, out _);

        Assert.Equal(expectedValid, isValid);
    }

    // Case 3: boundary values (calculation rule)
    [Fact]
    public void BoundaryValues_AreCalculatedCorrectly()
    {
        var requests = new List<DeliveryRequest>
        {
            new("R003", 1),
            new("R004", 50)
        };

        var summary = Pipeline.Run(requests, PricePolicies.Standard);

        Assert.Equal(600, summary.Quotes[0].Price);
        Assert.Equal(5500, summary.Quotes[1].Price);
    }

    // Case 4: invalid and missing input
    [Fact]
    public void InvalidAndMissingInput_AreRejected()
    {
        Assert.False(RequestParser.TryParse(null, out var missingRequest, out _));
        Assert.Null(missingRequest);

        Assert.False(RequestParser.TryParse(new RawRequest("R006", "abc"), out _, out var notNumber));
        Assert.Contains("not a number", notNumber);

        Assert.False(RequestParser.TryParse(new RawRequest("R007", null), out _, out var noDistance));
        Assert.Contains("missing", noDistance);

        Assert.False(RequestParser.TryParse(new RawRequest(null, "5"), out _, out _));

        // null is passed deliberately to check that missing input is rejected
        Assert.Throws<ArgumentNullException>(() => Pipeline.Run(null!, PricePolicies.Standard));
    }

    // Case 5: two policies on the same input
    [Fact]
    public void TwoPolicies_GiveDifferentResultsOnSameInput()
    {
        var requests = new List<DeliveryRequest> { new("R001", 2) };

        var standard = Pipeline.Run(requests, PricePolicies.Standard);
        var express = Pipeline.Run(requests, PricePolicies.Express);

        Assert.Equal(700, standard.Quotes[0].Price);
        Assert.Equal(1100, express.Quotes[0].Price);
        Assert.NotEqual(standard.TotalPrice, express.TotalPrice);
    }

    // Case 6: purity of an individual stage
    [Fact]
    public void NormalizeRequests_DoesNotModifyCallerInput()
    {
        var requests = new List<DeliveryRequest>
        {
            new(" r001 ", 2),
            new("R001", 5)
        };

        var result = Pipeline.NormalizeRequests(requests);

        // the caller's list is unchanged
        Assert.Equal(2, requests.Count);
        Assert.Equal(" r001 ", requests[0].Id);

        // the result is a new, normalized collection
        Assert.Single(result);
        Assert.Equal("R001", result[0].Id);
        Assert.NotSame(requests, result);
    }
}