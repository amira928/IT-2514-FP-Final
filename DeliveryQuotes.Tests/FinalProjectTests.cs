using DeliveryQuotes.App;
using Xunit;

namespace DeliveryQuotes.Tests;

public class FinalProjectTests
{
    private static T Value<T>(Result<T> result) =>
        Assert.IsType<Result<T>.Success>(result).Value;

    private static IReadOnlyList<string> Errors<T>(Result<T> result) =>
        Assert.IsType<Result<T>.Failure>(result).Errors;

    private static string JoinedErrors<T>(Result<T> result) => string.Join(" | ", Errors(result));

    private static Customer NewCustomer(string id, string tier, int credit) =>
        Value(Customer.Create(new RawCustomer(id, "Name " + id, tier, credit.ToString())));

    private static readonly RawRequest[] Requests =
    {
        new("R001", "2"),
        new("R002", "10"),
        new("R003", "1"),
        new("R004", "50"),
        new("R005", "-3"),
        new("R006", "20"),
        new("R007", "21")
    };

    private static IReadOnlyList<Customer> Customers() => new[]
    {
        NewCustomer("C001", "Gold", 6000),
        NewCustomer("C002", "Regular", 2000),
        NewCustomer("C003", "Gold", 10000)
    };

    private static Result<OrderBook> Place(
        OrderBook book, IReadOnlyList<Customer> customers, string customerId, string requestId) =>
        OrderWorkflow.Place(book, customers, Requests, customerId, requestId,
            PricePolicies.Standard, DiscountPolicies.CreateTierDiscount(10));

    private static ApprovedOrder PlaceOne(string customerId, string requestId) =>
        Value(Place(OrderBook.Empty, Customers(), customerId, requestId)).Orders[0];

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Customer_Create_ValidInput_NormalizesValues()
    {
        var customer = Value(Customer.Create(new RawCustomer(" c001 ", " Aigerim ", "gold", "6000")));

        Assert.Equal("C001", customer.Id);
        Assert.Equal("Aigerim", customer.Name);
        Assert.Equal(CustomerTier.Gold, customer.Tier);
        Assert.Equal(6000, customer.CreditLimit);
    }

    [Fact]
    public void Customer_Create_InvalidInput_ReportsEveryReason()
    {
        var errors = Errors(Customer.Create(new RawCustomer(null, "", "Silver", "abc")));

        Assert.Equal(4, errors.Count);
        Assert.Contains("Customer is missing", Errors(Customer.Create(null))[0]);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("100000", true)]
    [InlineData("-1", false)]
    [InlineData("100001", false)]
    public void Customer_Create_CreditLimitBoundaries(string credit, bool expectedValid)
    {
        var result = Customer.Create(new RawCustomer("C001", "Aigerim", "Gold", credit));

        Assert.Equal(expectedValid, result is Result<Customer>.Success);
    }

    [Fact]
    public void Catalog_ParseLines_RejectsEmptyWrongHeaderAndWrongColumns()
    {
        Assert.Contains("empty", JoinedErrors(CustomerCatalog.ParseLines(Array.Empty<string>(), "x.csv")));
        Assert.Contains("expected header",
            JoinedErrors(CustomerCatalog.ParseLines(new[] { "Id,Name" }, "x.csv")));
        Assert.Contains("line 2: expected 4 columns but found 3",
            JoinedErrors(CustomerCatalog.ParseLines(
                new[] { CustomerCatalog.ExpectedHeader, "C001,Aigerim,Gold" }, "x.csv")));
    }

    [Fact]
    public void Catalog_Build_DuplicateIdIsRejected()
    {
        var raws = new[]
        {
            new RawCustomer("C001", "A", "Gold", "100"),
            new RawCustomer("c001", "B", "Regular", "200")
        };

        Assert.Contains("Duplicate customer id 'C001'", JoinedErrors(CustomerCatalog.Build(raws)));
    }

    [Fact]
    public void Catalog_FindById_ReturnsSomeOrNone()
    {
        var customers = Customers();

        var found = Assert.IsType<Maybe<Customer>.Some>(CustomerCatalog.FindById(customers, " c001 "));
        Assert.Equal("C001", found.Value.Id);
        Assert.IsType<Maybe<Customer>.None>(CustomerCatalog.FindById(customers, "C999"));
        Assert.IsType<Maybe<Customer>.None>(CustomerCatalog.FindById(customers, null));
    }

    [Fact]
    public void Discount_TwoConfiguredPolicies_GiveDifferentResultsOnSameInput()
    {
        var gold = NewCustomer("C001", "Gold", 6000);
        var regular = NewCustomer("C002", "Regular", 2000);
        var gold10 = DiscountPolicies.CreateTierDiscount(10);
        var gold20 = DiscountPolicies.CreateTierDiscount(20);

        Assert.Equal(1350, gold10(gold, 1500));
        Assert.Equal(1200, gold20(gold, 1500));
        Assert.Equal(1500, gold10(regular, 1500));
        Assert.Equal(1500, DiscountPolicies.NoDiscount(gold, 1500));
    }

    [Fact]
    public void Place_ValidOrder_AddsApprovedOrderAndKeepsOldBookUnchanged()
    {
        var before = OrderBook.Empty;

        var after = Value(Place(before, Customers(), "C001", "R002"));

        var order = Assert.Single(after.Orders);
        Assert.Equal(1500, order.ListPrice);
        Assert.Equal(1350, order.FinalPrice);
        Assert.Empty(before.Orders);
    }

    [Fact]
    public void Place_MissingCustomer_StopsLaterStepsWithOneError()
    {
        var errors = Errors(Place(OrderBook.Empty, Customers(), "C999", "R005"));

        Assert.Single(errors);
        Assert.Contains("Customer 'C999' was not found", errors[0]);
    }

    [Fact]
    public void Place_MissingOrInvalidRequest_IsRejectedWithReason()
    {
        Assert.Contains("Request 'R999' was not found",
            JoinedErrors(Place(OrderBook.Empty, Customers(), "C001", "R999")));
        Assert.Contains("out of range",
            JoinedErrors(Place(OrderBook.Empty, Customers(), "C001", "R005")));
    }

    [Fact]
    public void Place_OrderAboveCreditLimit_IsRejected()
    {
        var message = JoinedErrors(Place(OrderBook.Empty, Customers(), "C002", "R004"));

        Assert.Contains("exceeds the credit limit 2000", message);
    }

    [Fact]
    public void Place_CreditIsCumulative_FailedOrderLeavesBookUnchanged()
    {
        var customers = new[] { NewCustomer("C001", "Gold", 2000) };

        var book1 = Value(Place(OrderBook.Empty, customers, "C001", "R002"));
        var book2 = Value(Place(book1, customers, "C001", "R001"));
        var rejected = Place(book2, customers, "C001", "R003");

        Assert.Contains("exceeds the credit limit", JoinedErrors(rejected));
        Assert.Equal(2, book2.Orders.Count);
        Assert.Equal(1980, book2.SpentBy("C001"));
    }

    [Fact]
    public void Place_DuplicateOrder_IsRejectedAndBookIsUnchanged()
    {
        var book = Value(Place(OrderBook.Empty, Customers(), "C001", "R001"));

        var second = Place(book, Customers(), "C001", "R001");

        Assert.Contains("Order R001 already exists", JoinedErrors(second));
        Assert.Single(book.Orders);
    }

    [Fact]
    public void Dispatch_StandardOrder_AddsFeeToPrice()
    {
        var order = PlaceOne("C001", "R002");

        var plan = Value(DispatchWorkflow.Plan(order, ServiceType.Standard,
            (price, percent) => Result<int>.Ok(price * percent / 100)));

        Assert.Equal(135, plan.ServiceFee);
        Assert.Equal(1485, plan.Total);
    }

    [Fact]
    public void Dispatch_Express_IsAllowedUpTo20KmAndRejectedAbove()
    {
        var at20 = PlaceOne("C001", "R006");
        var at21 = PlaceOne("C001", "R007");

        Assert.IsType<Eligibility.Eligible>(DispatchWorkflow.CheckEligibility(at20, ServiceType.Express));
        var tooFar = Assert.IsType<Eligibility.TooFar>(DispatchWorkflow.CheckEligibility(at21, ServiceType.Express));
        Assert.Equal(21, tooFar.DistanceKm);
        Assert.Equal(20, tooFar.MaxKm);
        Assert.Contains("limited to 20 km", JoinedErrors(
            DispatchWorkflow.Plan(at21, ServiceType.Express, (p, pct) => Result<int>.Ok(0))));
    }

    [Fact]
    public void Dispatch_FeeFailure_PropagatesAndIneligibleOrderSkipsTheFeeCalculation()
    {
        var near = PlaceOne("C001", "R002");
        var far = PlaceOne("C003", "R004");
        int calls = 0;

        var failed = DispatchWorkflow.Plan(near, ServiceType.Standard,
            (p, pct) => { calls++; return Result<int>.Fail("native down"); });
        Assert.Contains("native down", JoinedErrors(failed));
        Assert.Equal(1, calls);

        calls = 0;
        var skipped = DispatchWorkflow.Plan(far, ServiceType.Express,
            (p, pct) => { calls++; return Result<int>.Ok(1); });
        Assert.IsType<Result<DispatchPlan>.Failure>(skipped);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Dispatch_RealNativeCall_CalculatesTheFee()
    {
        var order = PlaceOne("C001", "R002");

        var plan = Value(DispatchWorkflow.Plan(order, ServiceType.Standard));

        Assert.Equal(135, plan.ServiceFee);
        Assert.Equal(1485, plan.Total);
    }

    [Fact]
    public void Dispatch_NativeLoadFailure_IsAControlledFailure()
    {
        var order = PlaceOne("C001", "R002");

        var result = DispatchWorkflow.Plan(order, ServiceType.Standard,
            (price, percent) => NativeFeeAdapter.ServiceFee(
                price, percent, (_, _) => throw new DllNotFoundException("simulated")));

        Assert.Contains("could not be loaded", JoinedErrors(result));
    }

    [Fact]
    public void CustomerFile_Missing_ReturnsFailure()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.csv");

        Assert.Contains("not found", JoinedErrors(CustomerCatalog.LoadValidated(path)));
    }

    [Fact]
    public void CustomerFile_MalformedContent_ReturnsFailureWithLineNumber()
    {
        string dir = NewTempDir();
        try
        {
            string path = Path.Combine(dir, "customers.csv");
            File.WriteAllText(path, "Id,Name,Tier,CreditLimit\nC001,Aigerim,Gold\n");

            Assert.Contains("line 2: expected 4 columns", JoinedErrors(CustomerCatalog.LoadValidated(path)));

            File.WriteAllText(path, "Id,Name,Tier,CreditLimit\nC001,Aigerim,Silver,abc\n");
            Assert.Equal(2, Errors(CustomerCatalog.LoadValidated(path)).Count);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void FullWorkflow_FilesToOrderToDispatch_Succeeds()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "requests.csv"), "Id,Distance\nR002,10\n");
            File.WriteAllText(Path.Combine(dir, "customers.csv"), "Id,Name,Tier,CreditLimit\nC001,Aigerim,Gold,6000\n");

            var requests = Value(CsvLoader.Load(Path.Combine(dir, "requests.csv")));
            var customers = Value(CustomerCatalog.LoadValidated(Path.Combine(dir, "customers.csv")));

            var book = Value(OrderWorkflow.Place(OrderBook.Empty, customers, requests, "C001", "R002",
                PricePolicies.Standard, DiscountPolicies.CreateTierDiscount(10)));
            var plan = Value(DispatchWorkflow.Plan(book.Orders[0], ServiceType.Standard));

            Assert.Equal(1485, plan.Total);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void FullWorkflow_MissingCustomersFile_StopsTheWholeChain()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "requests.csv"), "Id,Distance\nR002,10\n");

            var outcome = CsvLoader.Load(Path.Combine(dir, "requests.csv"))
                .Bind(requests => CustomerCatalog.LoadValidated(Path.Combine(dir, "customers.csv"))
                    .Bind(customers => OrderWorkflow.Place(OrderBook.Empty, customers, requests, "C001", "R002",
                        PricePolicies.Standard, DiscountPolicies.NoDiscount)));

            Assert.Contains("not found", JoinedErrors(outcome));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}