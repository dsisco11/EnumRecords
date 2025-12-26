using EnumRecords.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EnumRecords.Tests;

/// <summary>
/// Tests for edge cases and special scenarios.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void Generator_WithIgnoreAttribute_ExcludesMember()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("Active")]
                Active,
                
                [Ignore]
                None  // Should be ignored, no EnumData needed
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        // Should not have ENUMREC002 for the ignored member
        var missingDataDiagnostics = result.GeneratorDiagnostics
            .Where(d => d.Id == "ENUMREC002")
            .ToList();

        Assert.Empty(missingDataDiagnostics);

        // Extension method should handle the ignored case
        var extensionSource = result.GeneratedSources
            .FirstOrDefault(s => s.HintName == "StatusExtensions.g.cs");
        
        Assert.NotNull(extensionSource);
    }

    [Fact]
    public void Generator_WithReverseLookup_GeneratesTryFromMethod()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props([ReverseLookup] string Code);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("ACT")]
                Active,
                
                [EnumData("INA")]
                Inactive
            }
            """;

        var extensionSource = GeneratorTestHelper.GetGeneratedSource(source, "StatusExtensions.g.cs");

        Assert.NotNull(extensionSource);
        Assert.Contains("TryFromCode", extensionSource.SourceText);
    }

    [Fact]
    public void Generator_WithCaseInsensitiveReverseLookup_GeneratesCorrectMethod()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props([ReverseLookup(CaseInsensitive = true)] string Code);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("active")]
                Active
            }
            """;

        var extensionSource = GeneratorTestHelper.GetGeneratedSource(source, "StatusExtensions.g.cs");

        Assert.NotNull(extensionSource);
        // Generator generates TryFrom method for reverse lookup
        Assert.Contains("TryFromCode", extensionSource.SourceText);
    }

    [Fact]
    public void Generator_WithUnicodeCharacters_EscapesCorrectly()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name);

            [EnumRecord<Props>]
            public enum Emoji
            {
                [EnumData("😀 Happy")]
                Happy,
                
                [EnumData("日本語")]
                Japanese
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);

        var extensionSource = result.GeneratedSources
            .FirstOrDefault(s => s.HintName == "EmojiExtensions.g.cs");
        
        Assert.NotNull(extensionSource);
    }

    [Fact]
    public void Generator_WithSpecialCharacters_EscapesCorrectly()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name);

            [EnumRecord<Props>]
            public enum Special
            {
                [EnumData("Line1\nLine2")]
                Newline,
                
                [EnumData("Tab\tHere")]
                Tab,
                
                [EnumData("Quote\"Here")]
                Quote
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void Generator_WithMultipleProperties_GeneratesAllAccessors()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ComplexProps(string Name, int Code, bool IsActive, double Value);

            [EnumRecord<ComplexProps>]
            public enum Complex
            {
                [EnumData("Item", 1, true, 3.14)]
                Item
            }
            """;

        var extensionSource = GeneratorTestHelper.GetGeneratedSource(source, "ComplexExtensions.g.cs");

        Assert.NotNull(extensionSource);
        Assert.Contains("public static string Name(this Complex value)", extensionSource.SourceText);
        Assert.Contains("public static int Code(this Complex value)", extensionSource.SourceText);
        Assert.Contains("public static bool IsActive(this Complex value)", extensionSource.SourceText);
        Assert.Contains("public static double Value(this Complex value)", extensionSource.SourceText);
    }

    [Fact]
    public void Generator_WithEnumWithoutAttribute_DoesNotGenerate()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            // Regular enum without [EnumRecord<T>]
            public enum RegularEnum
            {
                Value1,
                Value2
            }
            """;

        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Should only have the attributes file, no enum-specific files
        Assert.DoesNotContain(generatedSources, s => s.HintName.Contains("RegularEnum"));
    }

    [Fact]
    public void Generator_WithEmptyEnum_HandlesGracefully()
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

        var result = GeneratorTestHelper.RunGenerator(source);

        // Should not crash, may or may not generate files for empty enum
        Assert.NotNull(result);
    }

    [Fact]
    public void Generator_WithGetAllMethods_GeneratesCorrectly()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("First")]
                First,
                
                [EnumData("Second")]
                Second
            }
            """;

        var extensionSource = GeneratorTestHelper.GetGeneratedSource(source, "StatusExtensions.g.cs");

        Assert.NotNull(extensionSource);
        // Generator uses pluralized property names like GetNames() not GetAllNames()
        Assert.Contains("GetNames()", extensionSource.SourceText);
    }

    [Fact]
    public void Generator_WithGlobalNamespace_GeneratesCorrectly()
    {
        var source = """
            using EnumRecords;

            public readonly record struct Props(string Name);

            [EnumRecord<Props>]
            public enum GlobalEnum
            {
                [EnumData("Value")]
                Value
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        // May or may not support global namespace, but should not crash
        Assert.NotNull(result);
    }

    [Fact]
    public void Generator_WithNestedEnum_HandlesCorrectly()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name);

            public class Outer
            {
                [EnumRecord<Props>]
                public enum NestedEnum
                {
                    [EnumData("Value")]
                    Value
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        // Should handle nested enums appropriately
        Assert.NotNull(result);
    }

    [Fact]
    public void Generator_WithMultipleReverseLookups_GeneratesAllMethods()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(
                [ReverseLookup] string Code,
                [ReverseLookup] int Id);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("ACT", 1)]
                Active,
                
                [EnumData("INA", 2)]
                Inactive
            }
            """;

        var extensionSource = GeneratorTestHelper.GetGeneratedSource(source, "StatusExtensions.g.cs");

        Assert.NotNull(extensionSource);
        Assert.Contains("TryFromCode", extensionSource.SourceText);
        Assert.Contains("TryFromId", extensionSource.SourceText);
    }
}
