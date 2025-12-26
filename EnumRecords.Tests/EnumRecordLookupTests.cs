using EnumRecords.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EnumRecords.Tests;

/// <summary>
/// Tests for the central EnumRecord lookup class generation.
/// </summary>
public class EnumRecordLookupTests
{
    [Fact]
    public void Generator_WithValidEnum_GeneratesEnumRecordLookup()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ColorProps(string Hex);

            [EnumRecord<ColorProps>]
            public enum Color
            {
                [EnumData("#FF0000")]
                Red,
                
                [EnumData("#00FF00")]
                Green
            }
            """;

        var lookupSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecord.g.cs");

        Assert.NotNull(lookupSource);
        Assert.Contains("public static class EnumRecord", lookupSource.SourceText);
    }

    [Fact]
    public void Generator_WithMultipleEnums_GeneratesUnifiedLookup()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props1(string Value);
            public readonly record struct Props2(int Number);

            [EnumRecord<Props1>]
            public enum Enum1
            {
                [EnumData("A")]
                A
            }

            [EnumRecord<Props2>]
            public enum Enum2
            {
                [EnumData(42)]
                B
            }
            """;

        var lookupSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecord.g.cs");

        Assert.NotNull(lookupSource);
        Assert.Contains("public static class EnumRecord", lookupSource.SourceText);
    }

    [Fact]
    public void EnumRecordLookup_ContainsMethodsForEachEnum()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct StatusProps(string Name, int Code);

            [EnumRecord<StatusProps>]
            public enum Status
            {
                [EnumData("Active", 1)]
                Active,
                
                [EnumData("Inactive", 0)]
                Inactive
            }
            """;

        var lookupSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecord.g.cs");

        Assert.NotNull(lookupSource);
        // Should contain accessor for the Status enum
        Assert.Contains("Status()", lookupSource.SourceText);
    }

    [Fact]
    public void EnumRecordLookup_NotGeneratedForEmptyEnum()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name);

            [EnumRecord<Props>]
            public enum EmptyEnum
            {
                // No members
            }
            """;

        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // EnumRecord.g.cs should not be generated for empty enums
        Assert.DoesNotContain(generatedSources, s => s.HintName == "EnumRecord.g.cs");
    }

    [Fact]
    public void EnumRecordLookup_GeneratedOnlyOnce()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Value);

            [EnumRecord<Props>]
            public enum First { [EnumData("1")] One }

            [EnumRecord<Props>]
            public enum Second { [EnumData("2")] Two }

            [EnumRecord<Props>]
            public enum Third { [EnumData("3")] Three }
            """;

        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Should only have one EnumRecord.g.cs file
        var lookupFiles = generatedSources.Where(s => s.HintName == "EnumRecord.g.cs").ToList();
        Assert.Single(lookupFiles);
    }

    [Fact]
    public void EnumRecordLookup_CompilationHasNoErrors()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ItemProps(string Label, int Quantity);

            [EnumRecord<ItemProps>]
            public enum Item
            {
                [EnumData("Widget", 10)]
                Widget,
                
                [EnumData("Gadget", 20)]
                Gadget
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var lookupSource = result.GeneratedSources.FirstOrDefault(s => s.HintName == "EnumRecord.g.cs");

        Assert.NotNull(lookupSource);

        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }
}
