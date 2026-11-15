namespace DeliveryQuotes.App;

// Application boundary: the only place that touches the file system.
public static class CsvLoader
{
    public const string ExpectedHeader = "Id,Distance";

    // Reads the file. Operational problems (missing file, access, I/O) become a Failure.
    public static Result<IReadOnlyList<RawRequest>> Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<IReadOnlyList<RawRequest>>.Fail("File path is not specified.");
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (FileNotFoundException)
        {
            return Result<IReadOnlyList<RawRequest>>.Fail($"Data file not found: '{path}'.");
        }
        catch (DirectoryNotFoundException)
        {
            return Result<IReadOnlyList<RawRequest>>.Fail($"Folder of the data file not found: '{path}'.");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<IReadOnlyList<RawRequest>>.Fail($"Access to the data file is denied: '{path}'.");
        }
        catch (IOException ex)
        {
            return Result<IReadOnlyList<RawRequest>>.Fail($"Cannot read the data file '{path}': {ex.Message}");
        }

        return ParseLines(lines, path);
    }

    // Checks the structure of the content. All or nothing: any malformed line rejects the whole file.
    public static Result<IReadOnlyList<RawRequest>> ParseLines(IReadOnlyList<string> lines, string source)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            return Result<IReadOnlyList<RawRequest>>.Fail(
                $"{source}: the file is empty, the header '{ExpectedHeader}' is missing.");
        }

        if (!string.Equals(lines[0].Trim(), ExpectedHeader, StringComparison.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyList<RawRequest>>.Fail(
                $"{source}: line 1: expected header '{ExpectedHeader}' but found '{lines[0].Trim()}'.");
        }

        var requests = new List<RawRequest>();
        var errors = new List<string>();

        for (int i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            string[] parts = lines[i].Split(',');
            if (parts.Length != 2)
            {
                errors.Add($"{source}: line {i + 1}: expected 2 columns but found {parts.Length}.");
                continue;
            }

            requests.Add(new RawRequest(parts[0].Trim(), parts[1].Trim()));
        }

        if (errors.Count > 0)
        {
            return new Result<IReadOnlyList<RawRequest>>.Failure(errors);
        }

        return Result<IReadOnlyList<RawRequest>>.Ok(requests);
    }
}