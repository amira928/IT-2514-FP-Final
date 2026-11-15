namespace DeliveryQuotes.App;

public static class CustomerCatalog
{
    public const string ExpectedHeader = "Id,Name,Tier,CreditLimit";

    public static Result<IReadOnlyList<RawCustomer>> Load(string path) =>
        FileBoundary.ReadLines(path, "Customers")
            .Bind(lines => ParseLines(lines, path));

    public static Result<IReadOnlyList<RawCustomer>> ParseLines(IReadOnlyList<string> lines, string source)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            return Result<IReadOnlyList<RawCustomer>>.Fail(
                $"{source}: the file is empty, the header '{ExpectedHeader}' is missing.");
        }

        if (!string.Equals(lines[0].Trim(), ExpectedHeader, StringComparison.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyList<RawCustomer>>.Fail(
                $"{source}: line 1: expected header '{ExpectedHeader}' but found '{lines[0].Trim()}'.");
        }

        var customers = new List<RawCustomer>();
        var errors = new List<string>();

        for (int i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            string[] parts = lines[i].Split(',');
            if (parts.Length != 4)
            {
                errors.Add($"{source}: line {i + 1}: expected 4 columns but found {parts.Length}.");
                continue;
            }

            customers.Add(new RawCustomer(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim()));
        }

        if (errors.Count > 0)
        {
            return new Result<IReadOnlyList<RawCustomer>>.Failure(errors);
        }

        return Result<IReadOnlyList<RawCustomer>>.Ok(customers);
    }
    public static Result<IReadOnlyList<Customer>> Build(IReadOnlyList<RawCustomer> raws)
    {
        ArgumentNullException.ThrowIfNull(raws);

        var customers = new List<Customer>();
        var errors = new List<string>();

        foreach (var raw in raws)
        {
            switch (Customer.Create(raw))
            {
                case Result<Customer>.Success success:
                    customers.Add(success.Value);
                    break;
                case Result<Customer>.Failure failure:
                    errors.AddRange(failure.Errors);
                    break;
            }
        }

        errors.AddRange(customers
            .GroupBy(c => c.Id)
            .Where(g => g.Count() > 1)
            .Select(g => $"Duplicate customer id '{g.Key}'."));

        if (errors.Count > 0)
        {
            return new Result<IReadOnlyList<Customer>>.Failure(errors);
        }

        return Result<IReadOnlyList<Customer>>.Ok(customers);
    }

    public static Result<IReadOnlyList<Customer>> LoadValidated(string path) =>
        Load(path).Bind(raws => Build(raws));

    public static Maybe<Customer> FindById(IReadOnlyList<Customer> customers, string? id)
    {
        ArgumentNullException.ThrowIfNull(customers);

        if (string.IsNullOrWhiteSpace(id))
        {
            return Maybe<Customer>.Empty();
        }

        string key = id.Trim();
        foreach (var customer in customers)
        {
            if (string.Equals(customer.Id, key, StringComparison.OrdinalIgnoreCase))
            {
                return Maybe<Customer>.Of(customer);
            }
        }

        return Maybe<Customer>.Empty();
    }
}