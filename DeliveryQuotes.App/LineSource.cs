namespace DeliveryQuotes.App;

public static class LineSource
{
    public static IEnumerable<string> ReadLines(Func<TextReader> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        return Iterate(open);
    }

    private static IEnumerable<string> Iterate(Func<TextReader> open)
    {
        using var reader = open();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }

    public static Result<IReadOnlyList<string>> Snapshot(string path)
    {
        try
        {
            var lines = ReadLines(() => new StreamReader(path)).ToArray();
            return Result<IReadOnlyList<string>>.Ok(lines);
        }
        catch (FileNotFoundException)
        {
            return Result<IReadOnlyList<string>>.Fail($"Data file not found: '{path}'.");
        }
        catch (DirectoryNotFoundException)
        {
            return Result<IReadOnlyList<string>>.Fail($"Folder of the data file not found: '{path}'.");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<IReadOnlyList<string>>.Fail($"Access to the data file is denied: '{path}'.");
        }
        catch (IOException ex)
        {
            return Result<IReadOnlyList<string>>.Fail($"Cannot read the data file '{path}': {ex.Message}");
        }
    }
}