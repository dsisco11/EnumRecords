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
        Console.WriteLine("Reverse lookup:");
        
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
    }
}
