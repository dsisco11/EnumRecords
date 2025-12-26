# EnumRecords

A C# source generator that associates compile-time constant data properties with enum values, enabling property-like access via generated extension methods.

[![CI](https://github.com/dsisco11/EnumRecords/actions/workflows/ci.yml/badge.svg)](https://github.com/dsisco11/EnumRecords/actions/workflows/ci.yml)

## Features

- 🚀 **Zero runtime overhead** — All code is generated at compile time
- 📦 **No runtime dependencies** — Attributes are source-generated into your project
- 🔍 **IntelliSense support** — Full IDE autocomplete for generated extension methods
- ✅ **Type-safe** — Compile-time validation of property types and values
- 🎯 **Simple API** — Just two attributes to learn
- 🔄 **Reverse lookup** — Find enum values by property values with `[ReverseLookup]`
- 📋 **Collection access** — Get all property values via `Get{PropertyName}s()` methods

## Requirements

- .NET 6.0+ or .NET Standard 2.0+ consuming project
- C# 9.0+ (for record struct support in consuming code)

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

Apply `[EnumRecord<T>]` to your enum and `[EnumData(...)]` to each member:

```csharp
using EnumRecords;

[EnumRecord<ColorEnumProperties>]
public enum EColors : int
{
    [EnumData("Red", 1, "#FF0000")]
    Red = 1,

    [EnumData("Green", 2, "#00FF00")]
    Green = 2,

    [EnumData("Blue", 3, "#0000FF")]
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

The `[EnumData]` attribute accepts any compile-time constant values:

| Type                               | Example                   |
| ---------------------------------- | ------------------------- |
| `string`                           | `"Hello"`                 |
| `int`, `long`, `short`, `byte`     | `42`, `100L`              |
| `uint`, `ulong`, `ushort`, `sbyte` | `42U`, `100UL`            |
| `float`, `double`, `decimal`       | `3.14f`, `3.14d`, `3.14m` |
| `bool`                             | `true`, `false`           |
| `char`                             | `'A'`                     |

## Reverse Lookup

You can mark properties with `[ReverseLookup]` to generate methods that find an enum value by its property value:

### Setup

Add `[ReverseLookup]` to the constructor parameter in your properties record struct:

```csharp
public readonly record struct ColorEnumProperties(
    string Name,
    int Value,
    [ReverseLookup] string HexCode  // Enable reverse lookup for HexCode
);
```

### Generated Methods

For each property marked with `[ReverseLookup]`, two static methods are generated:

```csharp
// Non-throwing variant - returns false if not found
public static bool TryFromHexCode(string value, out EColors result);

// Throwing variant - throws ArgumentException if not found
public static EColors FromHexCode(string value);
```

### Usage

```csharp
// Find enum by property value (throwing)
EColors red = EColorsExtensions.FromHexCode("#FF0000");  // Returns EColors.Red

// Find enum by property value (non-throwing)
if (EColorsExtensions.TryFromHexCode("#00FF00", out var color))
{
    Console.WriteLine(color);  // Green
}

// Handle not found
if (!EColorsExtensions.TryFromHexCode("#FFFFFF", out _))
{
    Console.WriteLine("Color not found");
}

// Throwing variant for unknown values
try
{
    var unknown = EColorsExtensions.FromHexCode("#FFFFFF");
}
catch (ArgumentException ex)
{
    // "No EColors found with HexCode '#FFFFFF'"
}
```

### Uniqueness Requirement

Properties marked with `[ReverseLookup]` **must have unique values** across all enum members. The generator emits a compile-time error (`ENUMREC001`) if duplicate values are detected:

```csharp
// ❌ This will cause compile error ENUMREC001
public readonly record struct BadProps([ReverseLookup] string Code);

[EnumRecord<BadProps>]
public enum BadEnum
{
    [EnumData("A")]
    First,
    [EnumData("A")]  // Error: Duplicate value '"A"' for reverse-lookup property 'Code'
    Second,
}
```

### Case-Insensitive String Lookup

For string properties, you can enable case-insensitive lookups with `IgnoreCase = true`:

```csharp
public readonly record struct FileTypeProperties(
    string Extension,
    [ReverseLookup(IgnoreCase = true)] string MimeType
);

[EnumRecord<FileTypeProperties>]
public enum FileType
{
    [EnumData(".json", "application/json")]
    Json,
    [EnumData(".xml", "application/xml")]
    Xml,
}

// Usage - all these will match FileType.Json
FileTypeExtensions.FromMimeType("application/json");  // exact match
FileTypeExtensions.FromMimeType("APPLICATION/JSON");  // uppercase
FileTypeExtensions.FromMimeType("Application/Json");  // mixed case
```

> **Note:** When using `IgnoreCase = true`, the uniqueness check also uses case-insensitive comparison. For example, `"ABC"` and `"abc"` would be considered duplicates.

## Ignoring Enum Members

Use `[Ignore]` to exclude specific enum members from property mappings. This is useful for sentinel values like `None`, `Unknown`, or deprecated entries.

### Setup

```csharp
using EnumRecords;

[EnumRecord<FileTypeProperties>]
public enum FileType
{
    [Ignore]  // No properties required, excluded from all generated code
    Unknown = 0,

    [EnumData(".json", "application/json")]
    Json,

    [EnumData(".xml", "application/xml")]
    Xml,
}
```

### Behavior

Members marked with `[Ignore]`:

- **Do not require** `[EnumData]` — no compile error for missing properties
- **Are excluded from** generated extension methods — calling `.Extension()` on an ignored member throws `ArgumentOutOfRangeException`
- **Are excluded from** `Get{PropertyName}s()` collections
- **Are excluded from** reverse lookup methods

```csharp
// Ignored members throw when accessing properties
try
{
    var ext = FileType.Unknown.Extension();
}
catch (ArgumentOutOfRangeException)
{
    // Expected - Unknown is not mapped
}

// Ignored members are not in collections
var extensions = FileTypeExtensions.GetExtensions();  // [".json", ".xml"] - no Unknown

// Reverse lookup won't return ignored members
FileTypeExtensions.TryFromMimeType("unknown", out _);  // false
```

> **Note:** If you have a conflict with another `[Ignore]` attribute (e.g., from NUnit or MSTest), use the fully qualified name: `[EnumRecords.Ignore]`

## Get All Property Values

For each property in your record struct, the generator creates a `Get{PropertyName}s()` method that returns all defined values as a read-only list:

### Generated Methods

```csharp
// For ColorEnumProperties with Name, Value, and HexCode properties:
public static IReadOnlyList<string> GetNames();
public static IReadOnlyList<int> GetValues();
public static IReadOnlyList<string> GetHexCodes();
```

### Usage

```csharp
// Get all property values as collections
var names = EColorsExtensions.GetNames();       // ["Red", "Green", "Blue"]
var values = EColorsExtensions.GetValues();     // [1, 2, 3]
var hexCodes = EColorsExtensions.GetHexCodes(); // ["#FF0000", "#00FF00", "#0000FF"]

// Useful for validation, dropdowns, etc.
if (EColorsExtensions.GetHexCodes().Contains(userInput))
{
    // Valid hex code
}

// Or with FileType enum
var extensions = FileTypeExtensions.GetExtensions();   // [".json", ".xml", ".csv"]
var mimeTypes = FileTypeExtensions.GetMimeTypes();     // ["application/json", "application/xml", "text/csv"]
```

## Record Helper Classes

In addition to extension methods, the generator creates two types of helper classes for more object-oriented access patterns.

### Per-Enum Record Class

For each enum with `[EnumRecord<T>]`, a `{EnumName}Record` helper class is generated:

```csharp
// Generated for EColors enum
public sealed class EColorsRecord
{
    public string GetName(EColors value) => value.Name();
    public int GetValue(EColors value) => value.Value();
    public string GetHexCode(EColors value) => value.HexCode();

    public IReadOnlyList<string> GetNames() => EColorsExtensions.GetNames();
    public IReadOnlyList<int> GetValues() => EColorsExtensions.GetValues();
    public IReadOnlyList<string> GetHexCodes() => EColorsExtensions.GetHexCodes();

    // If [ReverseLookup] is used:
    public bool TryFromHexCode(string value, out EColors? result) => ...;
    public EColors FromHexCode(string value) => ...;
}
```

### Central EnumRecord Lookup

A static `EnumRecord` class provides access to all enum record helpers:

```csharp
// Get the helper for a specific enum
var colorsRecord = new EColorsRecord();

// Use it for property access
string name = colorsRecord.GetName(EColors.Red);  // "Red"
var allNames = colorsRecord.GetNames();           // ["Red", "Green", "Blue"]
```

### Use Cases

Record helper classes are useful when you need to:

- **Pass enum metadata as a dependency** — inject a helper instance rather than using static methods
- **Work with generic code** — use the helper in scenarios where extension methods are awkward
- **Test enum-related logic** — mock or substitute the helper for testing

```csharp
// Dependency injection example
public class ColorService
{
    private readonly EColorsRecord _colorRecord;

    public ColorService(EColorsRecord colorRecord)
    {
        _colorRecord = colorRecord;
    }

    public string GetColorInfo(EColors color)
    {
        return $"{_colorRecord.GetName(color)}: {_colorRecord.GetHexCode(color)}";
    }
}
```

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
    [EnumData(200, "OK", true)]
    Ok = 200,

    [EnumData(201, "Created", true)]
    Created = 201,

    [EnumData(400, "Bad Request", false)]
    BadRequest = 400,

    [EnumData(404, "Not Found", false)]
    NotFound = 404,

    [EnumData(500, "Internal Server Error", false)]
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
    [EnumData(".json", "application/json", "JSON Document")]
    Json,

    [EnumData(".xml", "application/xml", "XML Document")]
    Xml,

    [EnumData(".csv", "text/csv", "Comma-Separated Values")]
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

### `EnumDataAttribute`

Specifies the property values for an enum member.

```csharp
[EnumData(arg1, arg2, ...)]
EnumMember = value,
```

- Arguments are positional and must match the order of the properties record struct's constructor parameters
- All arguments must be compile-time constants

### `ReverseLookupAttribute`

Marks a property for reverse lookup, enabling lookup of enum values by property value.

```csharp
public readonly record struct MyProperties([ReverseLookup] string UniqueId);
```

- Applied to constructor parameters of the properties record struct
- Property values must be unique across all enum members (enforced at compile time)
- Generates `TryFrom{PropertyName}` and `From{PropertyName}` static methods

**Properties:**

| Property     | Type   | Default | Description                                         |
| ------------ | ------ | ------- | --------------------------------------------------- |
| `IgnoreCase` | `bool` | `false` | Enable case-insensitive matching for string lookups |

### `IgnoreAttribute`

Excludes an enum member from property mappings and generated code.

```csharp
[Ignore]
EnumMember = value,
```

- Applied to enum members (fields)
- Member does not require `[EnumData]`
- Excluded from extension methods, collections, and reverse lookups

### Generated Classes

For each enum decorated with `[EnumRecord<T>]`, the generator produces:

| Generated Type         | Description                                                        |
| ---------------------- | ------------------------------------------------------------------ |
| `{EnumName}Extensions` | Static extension methods for property access                       |
| `{EnumName}Record`     | Instance helper class with the same methods as the extensions      |
| `EnumRecord` (once)    | Central static class providing access to all registered enum types |

### Nullability Attributes

Generated `TryFrom*` methods include proper nullability annotations:

```csharp
public static bool TryFromHexCode(
    string value,
    [NotNullWhen(true)] out EColors? result);
```

- `[NotNullWhen(true)]` indicates that `result` is non-null when the method returns `true`
- The out parameter is nullable (`EColors?`) and returns `null` on lookup failure

## License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
