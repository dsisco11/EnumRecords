using EnumRecords;

namespace EnumRecords.Tests;

// Define the properties record struct with reverse lookup on HexCode
public readonly record struct ColorEnumProperties(string Name, int Value, [ReverseLookup] string HexCode);

// Define the enum with associated properties
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

// Example with case-insensitive reverse lookup
public readonly record struct FileTypeProperties(
    string Extension,
    [ReverseLookup(IgnoreCase = true)] string MimeType
);

[EnumRecord<FileTypeProperties>]
public enum FileType
{
    [EnumRecordProperties(".json", "application/json")]
    Json,
    [EnumRecordProperties(".xml", "application/xml")]
    Xml,
    [EnumRecordProperties(".csv", "text/csv")]
    Csv,
}

// Test enum with errors (for diagnostic testing - uncomment to test)
// public readonly record struct TestErrorProperties(string Name, int Value, string Code);
// 
// [EnumRecord<TestErrorProperties>]
// public enum TestErrorEnum
// {
//     [EnumRecordProperties("First", 1)]  // Missing third argument - ENUMREC003
//     First,
//     [EnumRecordProperties("Second", 2, "S")]
//     Second,
//     Third,  // Missing attribute entirely - ENUMREC002
// }

public class Program
{
    public static void Main()
    {
        Console.WriteLine("EnumRecords Test");
        Console.WriteLine("================");
        Console.WriteLine();

        foreach (EColors color in Enum.GetValues<EColors>())
        {
            Console.WriteLine($"{color}:");
            Console.WriteLine($"  Name:    {color.Name()}");
            Console.WriteLine($"  Value:   {color.Value()}");
            Console.WriteLine($"  HexCode: {color.HexCode()}");
            Console.WriteLine();
        }

        // Direct access examples
        Console.WriteLine("Direct access:");
        Console.WriteLine($"EColors.Red.HexCode() = {EColors.Red.HexCode()}");
        Console.WriteLine($"EColors.Green.Name() = {EColors.Green.Name()}");
        Console.WriteLine($"EColors.Blue.Value() = {EColors.Blue.Value()}");
        Console.WriteLine();

        // Reverse lookup examples
        Console.WriteLine("Reverse lookup (case-sensitive):");
        
        // Using From (throwing variant)
        var redFromHex = EColorsExtensions.FromHexCode("#FF0000");
        Console.WriteLine($"FromHexCode(\"#FF0000\") = {redFromHex}");

        // Using TryFrom (non-throwing variant)
        if (EColorsExtensions.TryFromHexCode("#00FF00", out var greenFromHex))
        {
            Console.WriteLine($"TryFromHexCode(\"#00FF00\") = {greenFromHex}");
        }

        // TryFrom with non-existent value
        if (!EColorsExtensions.TryFromHexCode("#FFFFFF", out _))
        {
            Console.WriteLine("TryFromHexCode(\"#FFFFFF\") = not found (as expected)");
        }

        // From with non-existent value (throws)
        try
        {
            EColorsExtensions.FromHexCode("#FFFFFF");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"FromHexCode(\"#FFFFFF\") threw: {ex.Message}");
        }
        Console.WriteLine();

        // Case-insensitive reverse lookup examples
        Console.WriteLine("Reverse lookup (case-insensitive):");
        
        // Case matches exactly
        var json = FileTypeExtensions.FromMimeType("application/json");
        Console.WriteLine($"FromMimeType(\"application/json\") = {json}");

        // Different case - should still match
        var xmlUpper = FileTypeExtensions.FromMimeType("APPLICATION/XML");
        Console.WriteLine($"FromMimeType(\"APPLICATION/XML\") = {xmlUpper}");

        // Mixed case
        if (FileTypeExtensions.TryFromMimeType("Text/CSV", out var csv))
        {
            Console.WriteLine($"TryFromMimeType(\"Text/CSV\") = {csv}");
        }
    }
}
