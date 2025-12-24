using EnumRecords;

namespace EnumRecords.Tests;

// Define the properties record struct
public readonly record struct ColorEnumProperties(string Name, int Value, string HexCode);

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
    }
}
