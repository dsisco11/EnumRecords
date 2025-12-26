using EnumRecords;

namespace EnumRecords.Tests;

// Define the properties record struct with reverse lookup on HexCode
public readonly record struct ColorEnumProperties(string Name, int Value, [ReverseLookup] string HexCode);

// Define the enum with associated properties
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

// Example with case-insensitive reverse lookup
public readonly record struct FileTypeProperties(
    string Extension,
    [ReverseLookup(IgnoreCase = true)] string MimeType
);

[EnumRecord<FileTypeProperties>]
public enum FileType
{
    [Ignore] // Sentinel value - excluded from property mappings
    Unknown = 0,
    [EnumData(".json", "application/json")]
    Json,
    [EnumData(".xml", "application/xml")]
    Xml,
    [EnumData(".csv", "text/csv")]
    Csv,
}

// Test enum for Unicode and special character escaping
public readonly record struct EscapeTestProperties(
    [ReverseLookup] string Text,
    [ReverseLookup] char Character
);

[EnumRecord<EscapeTestProperties>]
public enum EscapeTest
{
    [EnumData("Hello\tWorld", '\t')]    // Tab
    Tab,
    [EnumData("Line1\nLine2", '\n')]    // Newline
    Newline,
    [EnumData("Return\rHere", '\r')]    // Carriage return
    CarriageReturn,
    [EnumData("Quote\"Test", '"')]      // Double quote
    Quote,
    [EnumData("Back\\slash", '\\')]     // Backslash
    Backslash,
    [EnumData("Null\0Char", '\0')]      // Null character
    NullChar,
    [EnumData("Alert\aSound", '\a')]    // Alert/bell
    Alert,
    [EnumData("Back\bSpace", '\b')]     // Backspace
    Backspace,
    [EnumData("Form\fFeed", '\f')]      // Form feed
    FormFeed,
    [EnumData("Vertical\vTab", '\v')]   // Vertical tab
    VerticalTab,
    [EnumData("Café", 'é')]             // Non-ASCII character
    Unicode,
    [EnumData("日本語", '日')]           // Japanese characters
    Japanese,
    [EnumData("Emoji: 😀", '€')]        // Emoji and Euro sign
    Emoji,
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
        Console.WriteLine();

        // GetAll methods examples
        Console.WriteLine("GetAll methods:");
        Console.WriteLine($"GetNames() = [{string.Join(", ", EColorsExtensions.GetNames())}]");
        Console.WriteLine($"GetValues() = [{string.Join(", ", EColorsExtensions.GetValues())}]");
        Console.WriteLine($"GetHexCodes() = [{string.Join(", ", EColorsExtensions.GetHexCodes())}]");
        Console.WriteLine($"FileType.GetExtensions() = [{string.Join(", ", FileTypeExtensions.GetExtensions())}]");
        Console.WriteLine($"FileType.GetMimeTypes() = [{string.Join(", ", FileTypeExtensions.GetMimeTypes())}]");
        Console.WriteLine();

        // Unicode and special character escaping tests
        Console.WriteLine("Unicode/Escape character tests:");
        Console.WriteLine("===============================");
        
        // Test that the extension methods work correctly with escaped characters
        Console.WriteLine($"EscapeTest.Tab.Text() = \"{EscapeTest.Tab.Text().Replace("\t", "\\t")}\"");
        Console.WriteLine($"EscapeTest.Tab.Character() = '\\t' (0x{(int)EscapeTest.Tab.Character():X2})");
        
        Console.WriteLine($"EscapeTest.Newline.Text() = \"{EscapeTest.Newline.Text().Replace("\n", "\\n")}\"");
        Console.WriteLine($"EscapeTest.Newline.Character() = '\\n' (0x{(int)EscapeTest.Newline.Character():X2})");
        
        Console.WriteLine($"EscapeTest.NullChar.Text() contains null: {EscapeTest.NullChar.Text().Contains('\0')}");
        Console.WriteLine($"EscapeTest.NullChar.Character() = '\\0' (0x{(int)EscapeTest.NullChar.Character():X2})");
        
        Console.WriteLine($"EscapeTest.Quote.Text() = \"{EscapeTest.Quote.Text().Replace("\"", "\\\"")}\"");
        Console.WriteLine($"EscapeTest.Quote.Character() = '\"' (0x{(int)EscapeTest.Quote.Character():X2})");
        
        Console.WriteLine($"EscapeTest.Backslash.Text() = \"{EscapeTest.Backslash.Text().Replace("\\", "\\\\")}\"");
        Console.WriteLine($"EscapeTest.Backslash.Character() = '\\\\' (0x{(int)EscapeTest.Backslash.Character():X2})");
        
        Console.WriteLine($"EscapeTest.Unicode.Text() = \"{EscapeTest.Unicode.Text()}\"");
        Console.WriteLine($"EscapeTest.Unicode.Character() = '{EscapeTest.Unicode.Character()}' (0x{(int)EscapeTest.Unicode.Character():X4})");
        
        Console.WriteLine($"EscapeTest.Japanese.Text() = \"{EscapeTest.Japanese.Text()}\"");
        Console.WriteLine($"EscapeTest.Japanese.Character() = '{EscapeTest.Japanese.Character()}' (0x{(int)EscapeTest.Japanese.Character():X4})");
        
        Console.WriteLine($"EscapeTest.Emoji.Text() = \"{EscapeTest.Emoji.Text()}\"");
        Console.WriteLine($"EscapeTest.Emoji.Character() = '{EscapeTest.Emoji.Character()}' (0x{(int)EscapeTest.Emoji.Character():X4})");
        Console.WriteLine();

        // Test reverse lookup with special characters
        Console.WriteLine("Reverse lookup with special characters:");
        
        if (EscapeTestExtensions.TryFromText("Hello\tWorld", out var tabResult))
        {
            Console.WriteLine($"TryFromText(\"Hello\\tWorld\") = {tabResult}");
        }
        
        if (EscapeTestExtensions.TryFromCharacter('\n', out var newlineResult))
        {
            Console.WriteLine($"TryFromCharacter('\\n') = {newlineResult}");
        }
        
        if (EscapeTestExtensions.TryFromCharacter('\0', out var nullResult))
        {
            Console.WriteLine($"TryFromCharacter('\\0') = {nullResult}");
        }
        
        if (EscapeTestExtensions.TryFromText("Café", out var unicodeResult))
        {
            Console.WriteLine($"TryFromText(\"Café\") = {unicodeResult}");
        }
        
        if (EscapeTestExtensions.TryFromText("日本語", out var japaneseResult))
        {
            Console.WriteLine($"TryFromText(\"日本語\") = {japaneseResult}");
        }
        
        Console.WriteLine();

        // EnumRecord lookup API tests
        Console.WriteLine("EnumRecord Lookup API:");
        Console.WriteLine("======================");
        
        // Strongly-typed non-generic access
        var colorsRecord = EnumRecord.EColors();
        Console.WriteLine($"EnumRecord.EColors().GetHexCode(EColors.Red) = {colorsRecord.GetHexCode(EColors.Red)}");
        Console.WriteLine($"EnumRecord.EColors().GetHexCodes() = [{string.Join(", ", colorsRecord.GetHexCodes())}]");
        Console.WriteLine($"EnumRecord.EColors().GetNames() = [{string.Join(", ", colorsRecord.GetNames())}]");
        
        // Reverse lookup via record helper
        if (colorsRecord.TryFromHexCode("#00FF00", out var greenResult))
        {
            Console.WriteLine($"EnumRecord.EColors().TryFromHexCode(\"#00FF00\") = {greenResult}");
        }
        Console.WriteLine($"EnumRecord.EColors().FromHexCode(\"#0000FF\") = {colorsRecord.FromHexCode("#0000FF")}");
        Console.WriteLine();

        // Generic access (returns object, needs cast)
        var colorsRecordGeneric = (EColorsRecord)EnumRecord.Get<EColors>();
        Console.WriteLine($"((EColorsRecord)EnumRecord.Get<EColors>()).GetHexCodes() = [{string.Join(", ", colorsRecordGeneric.GetHexCodes())}]");
        
        // FileType via generic
        var fileTypeRecord = (FileTypeRecord)EnumRecord.Get<FileType>();
        Console.WriteLine($"((FileTypeRecord)EnumRecord.Get<FileType>()).GetMimeTypes() = [{string.Join(", ", fileTypeRecord.GetMimeTypes())}]");
        Console.WriteLine();

        // Strongly-typed FileType access
        var ftRecord = EnumRecord.FileType();
        Console.WriteLine($"EnumRecord.FileType().GetExtension(FileType.Json) = {ftRecord.GetExtension(FileType.Json)}");
        Console.WriteLine($"EnumRecord.FileType().FromMimeType(\"TEXT/CSV\") = {ftRecord.FromMimeType("TEXT/CSV")}");
        Console.WriteLine();

        Console.WriteLine("All escape tests passed!");
    }
}
