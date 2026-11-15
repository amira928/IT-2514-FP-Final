namespace DeliveryQuotes.App;

public static class ReportExporter
{
    public static Result<string> ExportBandTotals(IReadOnlyList<BandTotal> totals, string path)
    {
        ArgumentNullException.ThrowIfNull(totals);

        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<string>.Fail("Export path is not specified.");
        }

        string temp = path + ".tmp";
        try
        {
            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using (var writer = new StreamWriter(temp))
            {
                writer.WriteLine("Band,Count,TotalPrice");
                foreach (var total in totals)
                {
                    writer.WriteLine($"{total.Band},{total.Count},{total.TotalPrice}");
                }
            }

            File.Move(temp, path, overwrite: true);
            return Result<string>.Ok(path);
        }
        catch (UnauthorizedAccessException)
        {
            return Result<string>.Fail($"Access denied while exporting to '{path}'.");
        }
        catch (DirectoryNotFoundException)
        {
            return Result<string>.Fail($"Folder not found while exporting to '{path}'.");
        }
        catch (IOException ex)
        {
            return Result<string>.Fail($"Cannot export the report to '{path}': {ex.Message}");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Result<string>.Fail($"Invalid export path '{path}': {ex.Message}");
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}