using System.Text.Json;
using System.Text.Json.Serialization;
using YoFi.V3.Application.Helpers;

/// <summary>
/// Console tool for parsing OFX files and dumping the results as JSON.
/// </summary>
/// <remarks>
/// Usage: OfxDump &lt;file-path&gt;
///
/// Parses the specified OFX file and outputs the resulting transactions and errors
/// as pretty-printed JSON to the console.
/// </remarks>
class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: OfxDump <file-path>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Parses an OFX file and outputs the parsing results as JSON.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example:");
            Console.Error.WriteLine("  OfxDump mybank.ofx");
            Console.Error.WriteLine("  OfxDump C:\\Downloads\\statement.ofx");
            return 1;
        }

        var filePath = args[0];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        try
        {
            // Parse the OFX file
            using var stream = File.OpenRead(filePath);
            var fileName = Path.GetFileName(filePath);
            var result = await OfxParsingHelper.ParseAsync(stream, fileName);

            // Configure JSON serialization for pretty printing
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Output the result as JSON
            var json = JsonSerializer.Serialize(result, options);
            Console.WriteLine(json);

            // Return exit code based on whether there were parsing errors
            return result.Errors.Count > 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
