using DeliveryQuotes.App;

string path = args.Length > 0 ? args[0] : "data/requests.csv";

var loaded = CsvLoader.Load(path);

if (loaded is Result<IReadOnlyList<RawRequest>>.Failure loadFailure)
{
    Console.WriteLine("DATA FILE PROBLEM:");
    foreach (var message in loadFailure.Errors)
    {
        Console.WriteLine($"  {message}");
    }
    return;
}

if (loaded is not Result<IReadOnlyList<RawRequest>>.Success loadSuccess)
{
    Console.WriteLine("Unknown load result.");
    return;
}

var rawRequests = loadSuccess.Value;
var customersResult = CustomerCatalog.LoadValidated("data/customers.csv");

if (customersResult is Result<IReadOnlyList<Customer>>.Failure customersFailure)
{
    Console.WriteLine("CUSTOMER FILE PROBLEM:");
    foreach (var problem in customersFailure.Errors)
    {
        Console.WriteLine($"  {problem}");
    }
    return;
}

if (customersResult is not Result<IReadOnlyList<Customer>>.Success customersSuccess)
{
    Console.WriteLine("Unknown customer load result.");
    return;
}

var customers = customersSuccess.Value;

var validRequests = new List<DeliveryRequest>();
var errors = new List<string>();

foreach (var raw in rawRequests)
{
    switch (ValidatedRequest.Create(raw))
    {
        case Result<ValidatedRequest>.Success success:
            validRequests.Add(new DeliveryRequest(success.Value.Id, success.Value.DistanceKm));
            break;
        case Result<ValidatedRequest>.Failure failure:
            errors.AddRange(failure.Errors);
            break;
    }
}

PrintSummary("Standard policy", Pipeline.Run(validRequests, PricePolicies.Standard));
PrintSummary("Express policy", Pipeline.Run(validRequests, PricePolicies.Express));

Console.WriteLine("Rejected records:");
foreach (var message in errors)
{
    Console.WriteLine($"  {message}");
}
Console.WriteLine();

Console.WriteLine("=== Quote workflow (Standard policy) ===");
foreach (var raw in rawRequests)
{
    Console.WriteLine($"  {QuoteWorkflow.Describe(QuoteWorkflow.Run(raw, PricePolicies.Standard))}");
}
Console.WriteLine();

Console.WriteLine("=== Assignment 7 demonstrations ===");

var success1 = QuoteWorkflow.RunById(rawRequests, "r002", PricePolicies.Standard);
Console.WriteLine($"  1. Success (Find, Bind, Map):  {QuoteWorkflow.DescribeView(success1)}");

var failure1 = QuoteWorkflow.RunById(rawRequests, "R005", PricePolicies.Standard);
Console.WriteLine($"  2. Domain failure (range):     {QuoteWorkflow.DescribeView(failure1)}");

var wrongOrder = QuoteWorkflow.Start(new RawRequest("DEMO1", "5"))
    .Bind(state => QuoteWorkflow.Confirm(state));
Console.WriteLine($"  3. Domain failure (state):     {QuoteWorkflow.Describe(wrongOrder)}");

var failure3 = QuoteWorkflow.RunById(rawRequests, "R999", PricePolicies.Standard);
Console.WriteLine($"  4. Domain failure (not found): {QuoteWorkflow.DescribeView(failure3)}");

Console.WriteLine($"  5. Lookup present: {DescribeLookup(rawRequests, "R001")}");
Console.WriteLine($"  6. Lookup absent:  {DescribeLookup(rawRequests, "R999")}");

Console.WriteLine("  7. Missing file:");
PrintLoad(CsvLoader.Load("data/no_such_file.csv"));

Console.WriteLine("  8. Malformed file:");
PrintLoad(CsvLoader.Load("data/malformed.csv"));
var feeResult = NativeFeeAdapter.ServiceFee(1500, 10);
Console.WriteLine(feeResult switch
{
    Result<int>.Success s => $"  9. Native fee for 1500 at 10%: {s.Value}",
    Result<int>.Failure f => "  9. Native call failed: " + string.Join("; ", f.Errors),
    _ => "  9. unknown"
});
var standardSummary = Pipeline.Run(validRequests, PricePolicies.Standard);

Console.WriteLine();
Console.WriteLine("=== Report 1: premium quotes (price >= 1000) ===");
foreach (var line in Reports.PremiumQuotes(standardSummary.Quotes, 1000))
{
    Console.WriteLine($"  {line.Id}: {line.DistanceKm} km -> {line.Price}");
}

Console.WriteLine("=== Report 2: totals by distance band ===");
foreach (var band in Reports.TotalsByBand(standardSummary.Quotes))
{
    Console.WriteLine($"  {band.Band}: count {band.Count}, total {band.TotalPrice}");
}
var relaxed = Scoring.CreateScore(10)(5);
var strict = Scoring.CreateScore(2)(20);

Console.WriteLine("=== Scoring with two configured functions ===");
Console.WriteLine("  relaxed (threshold 10 km, 5 per km):");
foreach (var item in Scoring.Rank(standardSummary.Quotes, relaxed))
{
    Console.WriteLine($"    {item.Id}: price {item.Price}, score {item.Score}");
}

Console.WriteLine("  strict (threshold 2 km, 20 per km):");
foreach (var item in Scoring.Rank(standardSummary.Quotes, strict))
{
    Console.WriteLine($"    {item.Id}: price {item.Price}, score {item.Score}");
}
var lazyQuotes = LazyQueries.LazyQuotes(rawRequests, PricePolicies.Standard);
var snapshot = lazyQuotes.ToArray();
Console.WriteLine("=== Lazy quotes (materialised once with ToArray) ===");
Console.WriteLine("  " + string.Join(", ", snapshot.Select(q => $"{q.Id}={q.Price}")));
Console.WriteLine("=== File lines (streamed, then materialised) ===");
Console.WriteLine(LineSource.Snapshot(path) switch
{
    Result<IReadOnlyList<string>>.Success s => $"  Snapshot of {path}: {s.Value.Count} lines",
    Result<IReadOnlyList<string>>.Failure f => "  Snapshot failed: " + string.Join("; ", f.Errors),
    _ => "  unknown"
});
Console.WriteLine("=== Export ===");
Console.WriteLine(ReportExporter.ExportBandTotals(Reports.TotalsByBand(standardSummary.Quotes), "output/band_totals.csv") switch
{
    Result<string>.Success s => $"  Report exported to {s.Value}",
    Result<string>.Failure f => "  Export failed: " + string.Join("; ", f.Errors),
    _ => "  unknown"
});
Console.WriteLine();
Console.WriteLine("=== WORKFLOW 2: place orders (Standard price, Gold discount 10%) ===");

var gold10 = DiscountPolicies.CreateTierDiscount(10);

var demoOrders = new[]
{
    ("C001", "R002"),
    ("C001", "R001"),
    ("C002", "R004"),
    ("C002", "R003"),
    ("C999", "R001"),
    ("C001", "R005"),
    ("C003", "R004"),
    ("C001", "R001")
};

var book = OrderBook.Empty;
foreach (var (customerId, requestId) in demoOrders)
{
    var placed = OrderWorkflow.Place(book, customers, rawRequests, customerId, requestId, PricePolicies.Standard, gold10);

    switch (placed)
    {
        case Result<OrderBook>.Success placedOk:
            book = placedOk.Value;
            var lastOrder = book.Orders.Last();
            Console.WriteLine($"  {customerId} + {requestId}: APPROVED, list {lastOrder.ListPrice}, final {lastOrder.FinalPrice}");
            break;
        case Result<OrderBook>.Failure placedBad:
            Console.WriteLine($"  {customerId} + {requestId}: REJECTED - {string.Join("; ", placedBad.Errors)}");
            break;
    }
}
Console.WriteLine($"  Orders in the book: {book.Orders.Count}");

Console.WriteLine();
Console.WriteLine("=== WORKFLOW 3: dispatch plan (fee from the native C library) ===");
foreach (var order in book.Orders)
{
    foreach (var service in new[] { ServiceType.Standard, ServiceType.Express })
    {
        var plan = DispatchWorkflow.Plan(order, service);
        var text = plan switch
        {
            Result<DispatchPlan>.Success planOk =>
                $"price {planOk.Value.Price} + fee {planOk.Value.ServiceFee} = total {planOk.Value.Total}",
            Result<DispatchPlan>.Failure planBad => "REJECTED - " + string.Join("; ", planBad.Errors),
            _ => "unknown"
        };
        Console.WriteLine($"  {order.OrderId} {service}: {text}");
    }
}

static string DescribeLookup(IReadOnlyList<RawRequest> requests, string id) =>
    RequestLookup.FindById(requests, id) switch
    {
        Maybe<RawRequest>.Some some => $"{id} found, distance text '{some.Value.DistanceText}'",
        Maybe<RawRequest>.None => $"{id} not found",
        _ => "unknown case"
    };

static void PrintLoad(Result<IReadOnlyList<RawRequest>> result)
{
    switch (result)
    {
        case Result<IReadOnlyList<RawRequest>>.Success success:
            Console.WriteLine($"     loaded {success.Value.Count} records");
            break;
        case Result<IReadOnlyList<RawRequest>>.Failure failure:
            foreach (var message in failure.Errors)
            {
                Console.WriteLine($"     {message}");
            }
            break;
    }
}

static void PrintSummary(string title, QuoteSummary summary)
{
    Console.WriteLine($"=== {title} ===");
    foreach (var quote in summary.Quotes)
    {
        Console.WriteLine($"  {quote.Id}: {quote.DistanceKm} km -> {quote.Price}");
    }
    Console.WriteLine($"  Count: {summary.Count}, Total: {summary.TotalPrice}");
    Console.WriteLine();
}