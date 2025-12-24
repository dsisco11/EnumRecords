# EnumRecords

A C# source generator that associates compile-time constant data properties with enum values, enabling property-like access via generated extension methods.

[![CI](https://github.com/dsisco11/EnumRecords/actions/workflows/ci.yml/badge.svg)](https://github.com/dsisco11/EnumRecords/actions/workflows/ci.yml)

## Features

- 🚀 **Zero runtime overhead** — All code is generated at compile time
- 📦 **No runtime dependencies** — Attributes are source-generated into your project
- 🔍 **IntelliSense support** — Full IDE autocomplete for generated extension methods
- ✅ **Type-safe** — Compile-time validation of property types and values
- 🎯 **Simple API** — Just two attributes to learn

## Installation

### NuGet Package

```bash
dotnet add package EnumRecords
```

> **Note:** EnumRecords is a development-only dependency. NuGet automatically configures it with `PrivateAssets="all"` so it won't become a transitive dependency of your consumers.

### Project Reference

For local development, add a reference to the generator project:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/EnumRecords.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Quick Start

### 1. Define a Properties Record Struct

Create a `readonly record struct` that defines the schema for your enum's associated data:

```csharp
public readonly record struct ColorEnumProperties(
    string Name,
    int Value,
    string HexCode
);
```

### 2. Decorate Your Enum

Apply `[EnumRecord<T>]` to your enum and `[EnumRecordProperties(...)]` to each member:

```csharp
using EnumRecords;

[EnumRecord<ColorEnumProperties>]
public enum EColors : int
{
    [EnumRecordProperties("Red", 1, "#FF0000")]
    Red = 1,

    [EnumRecordProperties("Green", 2, "#00FF00")]
    Green = 2,

    [EnumRecordProperties("Blue", 3, "#0000FF")]
    Blue = 3,
}
```

### 3. Access Properties via Extension Methods

The generator creates extension methods for each property in your record struct:

```csharp
// Access properties like methods on enum values
string hex = EColors.Red.HexCode();      // "#FF0000"
string name = EColors.Green.Name();      // "Green"
int value = EColors.Blue.Value();        // 3

// Works with variables too
EColors color = EColors.Red;
Console.WriteLine(color.HexCode());      // "#FF0000"

// Iterate over all values
foreach (EColors c in Enum.GetValues<EColors>())
{
    Console.WriteLine($"{c}: {c.Name()} - {c.HexCode()}");
}
```

## Supported Property Types

The `[EnumRecordProperties]` attribute accepts any compile-time constant values:

| Type                               | Example                   |
| ---------------------------------- | ------------------------- |
| `string`                           | `"Hello"`                 |
| `int`, `long`, `short`, `byte`     | `42`, `100L`              |
| `uint`, `ulong`, `ushort`, `sbyte` | `42U`, `100UL`            |
| `float`, `double`, `decimal`       | `3.14f`, `3.14d`, `3.14m` |
| `bool`                             | `true`, `false`           |
| `char`                             | `'A'`                     |

## Advanced Examples

### HTTP Status Codes

```csharp
public readonly record struct HttpStatusProperties(
    int Code,
    string Phrase,
    bool IsSuccess
);

[EnumRecord<HttpStatusProperties>]
public enum HttpStatus
{
    [EnumRecordProperties(200, "OK", true)]
    Ok = 200,

    [EnumRecordProperties(201, "Created", true)]
    Created = 201,

    [EnumRecordProperties(400, "Bad Request", false)]
    BadRequest = 400,

    [EnumRecordProperties(404, "Not Found", false)]
    NotFound = 404,

    [EnumRecordProperties(500, "Internal Server Error", false)]
    InternalServerError = 500,
}

// Usage
if (HttpStatus.Ok.IsSuccess())
{
    Console.WriteLine(HttpStatus.Ok.Phrase()); // "OK"
}
```

### File Types

```csharp
public readonly record struct FileTypeProperties(
    string Extension,
    string MimeType,
    string Description
);

[EnumRecord<FileTypeProperties>]
public enum FileType
{
    [EnumRecordProperties(".json", "application/json", "JSON Document")]
    Json,

    [EnumRecordProperties(".xml", "application/xml", "XML Document")]
    Xml,

    [EnumRecordProperties(".csv", "text/csv", "Comma-Separated Values")]
    Csv,
}

// Usage
string mime = FileType.Json.MimeType(); // "application/json"
```

## API Reference

### `EnumRecordAttribute<TProperties>`

Marks an enum as having associated record properties.

```csharp
[EnumRecord<TProperties>]
public enum MyEnum { ... }
```

- `TProperties` must be a `struct` (typically a `readonly record struct`)
- Applied to the enum declaration

### `EnumRecordPropertiesAttribute`

Specifies the property values for an enum member.

```csharp
[EnumRecordProperties(arg1, arg2, ...)]
EnumMember = value,
```

- Arguments are positional and must match the order of the properties record struct's constructor parameters
- All arguments must be compile-time constants

## Requirements

- .NET 6.0+ or .NET Standard 2.0+ consuming project
- C# 9.0+ (for record struct support in consuming code)

## Building from Source

```bash
# Clone the repository
git clone https://github.com/dsisco11/EnumRecords.git
cd EnumRecords

# Build the generator
dotnet build EnumRecords.csproj

# Build and run tests
dotnet run --project EnumRecords.Tests/EnumRecords.Tests.csproj
```

## How It Works

1. **Post-Initialization**: The generator emits the `EnumRecordAttribute<T>` and `EnumRecordPropertiesAttribute` types as source code into your compilation
2. **Syntax Analysis**: Finds all enums decorated with `[EnumRecord<T>]`
3. **Semantic Analysis**: Extracts the properties type `T` and reads constructor parameters to determine property names and types
4. **Code Generation**: For each decorated enum, generates a static extension class with one method per property using switch expressions

## License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
