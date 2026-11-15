namespace DeliveryQuotes.App;

public static class FileBoundary
{
    public static Result<IReadOnlyList<string>> ReadLines(string path, string what)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<IReadOnlyList<string>>.Fail($"{what}: file path is not specified.");
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            return Result<IReadOnlyList<string>>.Ok(lines);
        }
        catch (FileNotFoundException)
        {
            return Result<IReadOnlyList<string>>.Fail($"{what}: data file not found: '{path}'.");
        }
        catch (DirectoryNotFoundException)
        {
            return Result<IReadOnlyList<string>>.Fail($"{what}: folder of the data file not found: '{path}'.");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<IReadOnlyList<string>>.Fail($"{what}: access to the data file is denied: '{path}'.");
        }
        catch (IOException ex)
        {
            return Result<IReadOnlyList<string>>.Fail($"{what}: cannot read '{path}': {ex.Message}");
        }
    }
}