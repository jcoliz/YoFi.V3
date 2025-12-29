# OfxDump

Console tool for parsing OFX files and outputting the results as JSON.

## Purpose

This tool is useful for:
- Debugging OFX parsing issues
- Inspecting OFX file contents in a readable format
- Validating OFX files before importing
- Testing OFX parsing logic with real-world files

## Usage

```bash
# From repository root
dotnet run --project tools/OfxDump -- <file-path>

# Or build and run directly
cd tools/OfxDump
dotnet build
dotnet run -- <file-path>
```

## Examples

```bash
# Parse a local file
dotnet run --project tools/OfxDump -- mybank.ofx

# Parse an example file
dotnet run --project tools/OfxDump -- tests/Unit/SampleData/Ofx/Bank1.ofx

# Save output to file
dotnet run --project tools/OfxDump -- statement.ofx > output.json
```

## Output Format

The tool outputs a JSON object with the following structure:

```json
{
  "transactions": [
    {
      "date": "2023-11-15",
      "amount": -50.00,
      "payee": "Test Payee",
      "memo": "Transaction memo",
      "source": "Test Bank - Checking (9876543210)",
      "category": null
    }
  ],
  "errors": []
}
```

## Exit Codes

- `0` - Success, no parsing errors
- `1` - File not found or exception occurred
- `2` - Parsed successfully but with errors (check `errors` array in output)

## Implementation

Uses [`OfxParsingService`](../../src/Application/Import/Services/OfxParsingService.cs) from the Application layer, which wraps the OFXSharp library to parse both OFX 1.x (SGML) and OFX 2.x (XML) formats.
