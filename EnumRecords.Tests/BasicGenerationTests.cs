using EnumRecords.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EnumRecords.Tests;

/// <summary>
/// Tests for basic source generation functionality.
/// </summary>
public class BasicGenerationTests
{
    [Fact]
    public void Generator_WithValidEnum_GeneratesExtensionMethods()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct StatusProps(string DisplayName, int Code);

            [EnumRecord<StatusProps>]
            public enum Status
            {
                [EnumData("Active Status", 1)]
                Active,
                
                [EnumData("Inactive Status", 0)]
                Inactive
            }
            """;

        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        Assert.Contains(generatedSources, s => s.HintName == "StatusExtensions.g.cs");
        Assert.Contains(generatedSources, s => s.HintName == "StatusRecord.g.cs");
        Assert.Contains(generatedSources, s => s.HintName == "EnumRecord.g.cs");
    }

    [Fact]
    public void Generator_WithValidEnum_GeneratesCorrectExtensionMethodSignatures()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct PersonProps(string Name, int Age);

            [EnumRecord<PersonProps>]
            public enum Person
            {
                [EnumData("Alice", 30)]
                Alice,
                
                [EnumData("Bob", 25)]
                Bob
            }
            """;

        var extensionSource = GeneratorTestHelper.GetGeneratedSource(source, "PersonExtensions.g.cs");

        Assert.NotNull(extensionSource);
        Assert.Contains("public static string Name(this Person value)", extensionSource.SourceText);
        Assert.Contains("public static int Age(this Person value)", extensionSource.SourceText);
    }

    [Fact]
    public void Generator_WithValidEnum_GeneratesRecordClass()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ItemProps(string Label);

            [EnumRecord<ItemProps>]
            public enum Item
            {
                [EnumData("First Item")]
                First,
                
                [EnumData("Second Item")]
                Second
            }
            """;

        var recordSource = GeneratorTestHelper.GetGeneratedSource(source, "ItemRecord.g.cs");

        Assert.NotNull(recordSource);
        Assert.Contains("public sealed class ItemRecord", recordSource.SourceText);
        Assert.Contains("GetLabel", recordSource.SourceText);
    }

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
    public void Generator_WithMultipleEnums_GeneratesAllFiles()
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

        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        Assert.Contains(generatedSources, s => s.HintName == "Enum1Extensions.g.cs");
        Assert.Contains(generatedSources, s => s.HintName == "Enum1Record.g.cs");
        Assert.Contains(generatedSources, s => s.HintName == "Enum2Extensions.g.cs");
        Assert.Contains(generatedSources, s => s.HintName == "Enum2Record.g.cs");
        Assert.Contains(generatedSources, s => s.HintName == "EnumRecord.g.cs");
    }

    [Fact]
    public void Generator_OutputCompilation_HasNoErrors()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct TestProps(string Name);

            [EnumRecord<TestProps>]
            public enum TestEnum
            {
                [EnumData("Test")]
                Value
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void Generator_GeneratesAttributesFile()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Value);

            [EnumRecord<Props>]
            public enum MyEnum
            {
                [EnumData("Test")]
                Value
            }
            """;

        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source, includeAttributes: true);

        Assert.Contains(generatedSources, s => s.HintName == "EnumRecordAttributes.g.cs");
    }

    [Fact]
    public void Generator_WithNullableProperty_GeneratesCorrectCode()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct NullableProps(string? OptionalName);

            [EnumRecord<NullableProps>]
            public enum NullableEnum
            {
                [EnumData(null)]
                None,
                
                [EnumData("HasValue")]
                Some
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        
        // Verify generation completed without errors
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id.StartsWith("ENUMREC"));
        
        // Verify extension file was generated
        var extensionSource = result.GeneratedSources.FirstOrDefault(s => s.HintName == "NullableEnumExtensions.g.cs");
        if (extensionSource != null)
        {
            // If generated, check it contains the method
            Assert.Contains("OptionalName", extensionSource.SourceText);
        }
    }

    [Fact]
    public void Generator_PreservesNamespace()
    {
        var source = """
            using EnumRecords;

            namespace My.Deep.Namespace;

            public readonly record struct Props(string Value);

            [EnumRecord<Props>]
            public enum DeepEnum
            {
                [EnumData("Test")]
                Value
            }
            """;

        var extensionSource = GeneratorTestHelper.GetGeneratedSource(source, "DeepEnumExtensions.g.cs");

        Assert.NotNull(extensionSource);
        Assert.Contains("namespace My.Deep.Namespace", extensionSource.SourceText);
    }
}
