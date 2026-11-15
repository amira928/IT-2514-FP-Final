using DeliveryQuotes.App;
using Xunit;

namespace DeliveryQuotes.Tests;

public class ExceptionHandlingTests
{
    private static readonly IReadOnlyList<RawRequest> Data = new List<RawRequest>
    {
        new("R001", "2"),
        new("R002", "10"),
        new("R005", "-3")
    };

    private static string JoinedErrors<T>(Result<T> result)
    {
        var failure = Assert.IsType<Result<T>.Failure>(result);
        return string.Join(" | ", failure.Errors);
    }

    private static string TempCsv() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");

    [Fact]
    public void CompleteSuccess_ChainProducesConfirmedView()
    {
        var result = QuoteWorkflow.RunById(Data, "r002", PricePolicies.Standard);

        var success = Assert.IsType<Result<QuoteView>.Success>(result);
        Assert.Equal(new QuoteView("R002", "Confirmed", 1500), success.Value);
    }

    [Fact]
    public void DomainFailures_EachHasUsefulError()
    {
        var range = QuoteWorkflow.RunById(Data, "R005", PricePolicies.Standard);
        Assert.Contains("out of range", JoinedErrors(range));

        var state = QuoteWorkflow.Start(new RawRequest("DEMO1", "5"))
            .Bind(s => QuoteWorkflow.Confirm(s));
        Assert.Contains("price has not been calculated", JoinedErrors(state));

        var missing = QuoteWorkflow.RunById(Data, "R999", PricePolicies.Standard);
        var text = JoinedErrors(missing);
        Assert.Contains("R999", text);
        Assert.Contains("not found", text);
    }

    [Fact]
    public void Lookup_PresentAndAbsent()
    {
        var present = RequestLookup.FindById(Data, " r001 ");
        var some = Assert.IsType<Maybe<RawRequest>.Some>(present);
        Assert.Equal("2", some.Value.DistanceText);

        Assert.IsType<Maybe<RawRequest>.None>(RequestLookup.FindById(Data, "R999"));
        Assert.IsType<Maybe<RawRequest>.None>(RequestLookup.FindById(Data, ""));
    }

    [Fact]
    public void Lookup_LegitimatePrimitiveValuesAreNotAbsence()
    {
        var zero = Assert.IsType<Maybe<int>.Some>(Maybe<int>.Of(0));
        Assert.Equal(0, zero.Value);

        var no = Assert.IsType<Maybe<bool>.Some>(Maybe<bool>.Of(false));
        Assert.False(no.Value);

        var asResult = Assert.IsType<Result<int>.Success>(Maybe<int>.Of(0).ToResult("missing"));
        Assert.Equal(0, asResult.Value);

        Assert.IsType<Result<int>.Failure>(Maybe<int>.Empty().ToResult("missing"));
    }

    [Fact]
    public void Map_Success_ReceivesValueAndReturnsNewValue()
    {
        var result = Result<int>.Ok(5).Map(x => x * 2);

        var success = Assert.IsType<Result<int>.Success>(result);
        Assert.Equal(10, success.Value);
    }

    [Fact]
    public void Bind_Success_PreservesNextResult()
    {
        var next = Result<int>.Ok(5).Bind(x => Result<string>.Ok($"value {x}"));
        var success = Assert.IsType<Result<string>.Success>(next);
        Assert.Equal("value 5", success.Value);

        var rejected = Result<int>.Ok(5).Bind(x => Result<string>.Fail("rejected"));
        Assert.Contains("rejected", JoinedErrors(rejected));
    }

    [Fact]
    public void FailurePropagation_MapAndBindDoNotInvokeFunctions()
    {
        int calls = 0;
        var failure = Result<int>.Fail("boom");

        var mapped = failure.Map(x => { calls++; return x + 1; });
        var bound = failure.Bind(x => { calls++; return Result<int>.Ok(x); });

        Assert.Equal("boom", JoinedErrors(mapped));
        Assert.Equal("boom", JoinedErrors(bound));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void FailurePropagation_MissingItemSkipsPricePolicy()
    {
        int policyCalls = 0;

        var result = QuoteWorkflow.RunById(Data, "R999", km => { policyCalls++; return km; });

        Assert.IsType<Result<QuoteView>.Failure>(result);
        Assert.Equal(0, policyCalls);
    }

    [Fact]
    public void MissingFile_ReportsProblemAndPath()
    {
        string path = TempCsv();

        var result = CsvLoader.Load(path);

        var text = JoinedErrors(result);
        Assert.Contains("not found", text);
        Assert.Contains(path, text);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void MalformedFile_BecomesFailureWithoutPartialData()
    {
        string path = TempCsv();
        try
        {
            File.WriteAllLines(path, new[] { "Id,Distance", "R001,2", "R002", "R003,5,extra" });

            var result = CsvLoader.Load(path);

            var failure = Assert.IsType<Result<IReadOnlyList<RawRequest>>.Failure>(result);
            Assert.Equal(2, failure.Errors.Count);
            Assert.Contains("line 3", failure.Errors[0]);
            Assert.Contains("line 4", failure.Errors[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MalformedContent_WrongHeaderAndEmptyFile()
    {
        var wrongHeader = CsvLoader.ParseLines(new[] { "Name,Km", "R001,2" }, "test.csv");
        Assert.Contains("expected header", JoinedErrors(wrongHeader));

        var empty = CsvLoader.ParseLines(Array.Empty<string>(), "test.csv");
        Assert.Contains("empty", JoinedErrors(empty));
    }

    [Fact]
    public void ValidFile_LoadsAndHeaderOnlyFileIsValidEmpty()
    {
        string full = TempCsv();
        string headerOnly = TempCsv();
        try
        {
            File.WriteAllLines(full, new[] { "Id,Distance", "R001,2", "R002,10" });
            File.WriteAllLines(headerOnly, new[] { "Id,Distance" });

            var loaded = Assert.IsType<Result<IReadOnlyList<RawRequest>>.Success>(CsvLoader.Load(full));
            Assert.Equal(2, loaded.Value.Count);

            var empty = Assert.IsType<Result<IReadOnlyList<RawRequest>>.Success>(CsvLoader.Load(headerOnly));
            Assert.Empty(empty.Value);
        }
        finally
        {
            File.Delete(full);
            File.Delete(headerOnly);
        }
    }

    [Fact]
    public void SuccessWithoutPayload_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string>.Ok(null!));
        Assert.Throws<ArgumentNullException>(() => Maybe<string>.Of(null!));
    }
}