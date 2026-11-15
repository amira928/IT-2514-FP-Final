using DeliveryQuotes.App;
using Xunit;

namespace DeliveryQuotes.Tests;

public class Assignment8Tests
{
    private static readonly Quote[] Quotes =
    {
        new("R001", 2, 700),
        new("R002", 10, 1500),
        new("R003", 1, 600),
        new("R004", 50, 5500)
    };

    private static string JoinedErrors<T>(Result<T> result)
    {
        var failure = Assert.IsType<Result<T>.Failure>(result);
        return string.Join(" | ", failure.Errors);
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class TrackingReader : StringReader
    {
        public bool Disposed { get; private set; }

        public TrackingReader(string text) : base(text) { }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingReader : TextReader
    {
        public bool Disposed { get; private set; }

        public override string? ReadLine() => throw new IOException("read failed");

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void Extension_OrderByMostExpensive_OrdersAndDoesNotMutate()
    {
        var source = new List<Quote> { new("B", 3, 1000), new("A", 3, 1000), new("C", 9, 2000) };

        var ordered = source.OrderByMostExpensive().Select(q => q.Id).ToArray();

        Assert.Equal(new[] { "C", "A", "B" }, ordered);
        Assert.Equal("B", source[0].Id);
    }

    [Theory]
    [InlineData(5, "Short")]
    [InlineData(6, "Medium")]
    [InlineData(20, "Medium")]
    [InlineData(21, "Long")]
    public void Extension_DistanceBand_FollowsBoundaries(int km, string expected)
    {
        Assert.Equal(expected, new Quote("X", km, 100).DistanceBand());
    }

    [Fact]
    public void Report_PremiumQuotes_NormalNoMatchesAndEmpty()
    {
        var normal = Reports.PremiumQuotes(Quotes, 1000);
        Assert.Equal(new[] { "R004", "R002" }, normal.Select(l => l.Id).ToArray());

        var boundary = Reports.PremiumQuotes(Quotes, 1500);
        Assert.Equal(2, boundary.Count);

        Assert.Empty(Reports.PremiumQuotes(Quotes, 99999));
        Assert.Empty(Reports.PremiumQuotes(Array.Empty<Quote>(), 1000));
    }

    [Fact]
    public void Report_PremiumQuotes_TiedPricesAreOrderedById()
    {
        var tied = new[] { new Quote("B", 3, 1000), new Quote("A", 3, 1000) };

        var report = Reports.PremiumQuotes(tied, 500);

        Assert.Equal(new[] { "A", "B" }, report.Select(l => l.Id).ToArray());
    }

    [Fact]
    public void Report_TotalsByBand_GroupsAndSums()
    {
        var report = Reports.TotalsByBand(Quotes);

        Assert.Equal(3, report.Count);
        Assert.Equal(new BandTotal("Long", 1, 5500), report[0]);
        Assert.Equal(new BandTotal("Medium", 1, 1500), report[1]);
        Assert.Equal(new BandTotal("Short", 2, 1300), report[2]);
    }

    [Fact]
    public void Report_TotalsByBand_TiedTotalsAreOrderedByBand_AndEmptyIsEmpty()
    {
        var tied = new[] { new Quote("S", 3, 1000), new Quote("M", 10, 1000) };

        var report = Reports.TotalsByBand(tied);

        Assert.Equal(new[] { "Medium", "Short" }, report.Select(b => b.Band).ToArray());
        Assert.Empty(Reports.TotalsByBand(Array.Empty<Quote>()));
    }

    [Fact]
    public void Scoring_TwoConfiguredFunctions_DifferOnSameInput()
    {
        var relaxed = Scoring.CreateScore(10)(5);
        var strict = Scoring.CreateScore(2)(20);

        var relaxedRank = Scoring.Rank(Quotes, relaxed);
        var strictRank = Scoring.Rank(Quotes, strict);

        Assert.Equal(5700, relaxedRank.First(s => s.Id == "R004").Score);
        Assert.Equal(6460, strictRank.First(s => s.Id == "R004").Score);
        Assert.Equal(1500, relaxedRank.First(s => s.Id == "R002").Score);
        Assert.Equal(1660, strictRank.First(s => s.Id == "R002").Score);
        Assert.Equal(700, relaxedRank.First(s => s.Id == "R001").Score);
        Assert.Equal(700, strictRank.First(s => s.Id == "R001").Score);
    }

    [Fact]
    public void Lazy_NoProcessingBeforeEnumeration_AndSnapshotIsStable()
    {
        int calls = 0;
        var raws = new[] { new RawRequest("R001", "2"), new RawRequest("R002", "10"), new RawRequest("R005", "-3") };

        var lazy = LazyQueries.LazyQuotes(raws, km => { calls++; return km; });
        Assert.Equal(0, calls);

        var snapshot = lazy.ToArray();
        Assert.Equal(2, calls);

        _ = snapshot.Sum(q => q.Price);
        _ = snapshot.Count();
        Assert.Equal(2, calls);

        _ = lazy.Count();
        Assert.Equal(4, calls);
    }

    [Fact]
    public void Resource_IsDisposedAfterFullEnumeration()
    {
        var reader = new TrackingReader("a\nb\nc");

        var lines = LineSource.ReadLines(() => reader).ToArray();

        Assert.Equal(new[] { "a", "b", "c" }, lines);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Resource_IsDisposedAfterEarlyStop()
    {
        var reader = new TrackingReader("a\nb\nc");

        var first = LineSource.ReadLines(() => reader).First();

        Assert.Equal("a", first);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Resource_IsDisposedAfterFailure()
    {
        var reader = new ThrowingReader();

        Assert.Throws<IOException>(() => LineSource.ReadLines(() => reader).ToArray());

        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Native_RealCall_ReturnsManuallyCalculatedValue()
    {
        var result = NativeFeeAdapter.ServiceFee(1500, 10);

        var success = Assert.IsType<Result<int>.Success>(result);
        Assert.Equal(150, success.Value);
    }

    [Fact]
    public void Native_InvalidArguments_AreRejectedWithoutNativeCall()
    {
        int calls = 0;

        var result = NativeFeeAdapter.ServiceFee(1500, 101, (p, c) => { calls++; return 0; });

        Assert.Contains("out of range", JoinedErrors(result));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Native_LoadFailure_BecomesFailureResult()
    {
        var result = NativeFeeAdapter.ServiceFee(1500, 10, (p, c) => throw new DllNotFoundException("test"));

        Assert.Contains("could not be loaded", JoinedErrors(result));
    }

    [Fact]
    public void Workflow_Normal_IntegratesReportsExportAndNative()
    {
        string dir = NewTempDir();
        try
        {
            string input = Path.Combine(dir, "in.csv");
            string output = Path.Combine(dir, "out", "totals.csv");
            File.WriteAllLines(input, new[] { "Id,Distance", "R001,2", "R002,10", "R005,-3" });

            var exported = CsvLoader.Load(input)
                .Map(raws => LazyQueries.LazyQuotes(raws, PricePolicies.Standard).ToArray())
                .Bind(quotes => ReportExporter.ExportBandTotals(Reports.TotalsByBand(quotes), output));

            Assert.IsType<Result<string>.Success>(exported);
            Assert.Equal(
                new[] { "Band,Count,TotalPrice", "Medium,1,1500", "Short,1,700" },
                File.ReadAllLines(output));

            var fee = NativeFeeAdapter.ServiceFee(2200, 10);
            var success = Assert.IsType<Result<int>.Success>(fee);
            Assert.Equal(220, success.Value);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Workflow_Failure_ReportsProblemAndPreservesState()
    {
        string dir = NewTempDir();
        try
        {
            string output = Path.Combine(dir, "out", "totals.csv");

            Result<string> Run(string input) => CsvLoader.Load(input)
                .Map(raws => LazyQueries.LazyQuotes(raws, PricePolicies.Standard).ToArray())
                .Bind(quotes => ReportExporter.ExportBandTotals(Reports.TotalsByBand(quotes), output));

            var missing = Run(Path.Combine(dir, "missing.csv"));
            Assert.Contains("not found", JoinedErrors(missing));

            string malformed = Path.Combine(dir, "bad.csv");
            File.WriteAllLines(malformed, new[] { "Id,Distance", "R001" });
            var broken = Run(malformed);
            Assert.Contains("expected 2 columns", JoinedErrors(broken));

            Assert.False(File.Exists(output));
            Assert.False(Directory.Exists(Path.Combine(dir, "out")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}