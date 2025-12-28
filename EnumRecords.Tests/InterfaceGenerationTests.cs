using EnumRecords.Tests.Helpers;
using Xunit;

namespace EnumRecords.Tests;

/// <summary>
/// Tests for the generated IHas{PropertyName} interfaces and Resolve functionality.
/// </summary>
public class InterfaceGenerationTests
{
    [Fact]
    public void GeneratesIHasPropertyInterfaces()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ColorProps(string Name, string HexCode, int Value);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red", "#FF0000", 1)]
                Red
            }
            """;

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecordInterfaces.g.cs");

        Assert.NotNull(generatedSource);
        Assert.Contains("namespace EnumRecords.Contracts;", generatedSource!.SourceText);
        Assert.Contains("public interface IHasName { }", generatedSource.SourceText);
        Assert.Contains("public interface IHasHexCode { }", generatedSource.SourceText);
        Assert.Contains("public interface IHasValue { }", generatedSource.SourceText);
    }

    [Fact]
    public void GeneratesAccessorStructs()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ColorProps(string Name, string HexCode);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red", "#FF0000")]
                Red
            }
            """;

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecord.g.cs");

        Assert.NotNull(generatedSource);
        Assert.Contains("public readonly struct ColorsAccessor : IEnumRecordAccessor<global::TestNamespace.Colors>", generatedSource!.SourceText);
        Assert.Contains("public object? GetPropertyValue(global::TestNamespace.Colors value, string propertyName)", generatedSource.SourceText);
        Assert.Contains("public string GetName(global::TestNamespace.Colors value)", generatedSource.SourceText);
        Assert.Contains("public string GetHexCode(global::TestNamespace.Colors value)", generatedSource.SourceText);
    }

    [Fact]
    public void GeneratesResolveMethod()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ColorProps(string Name);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red")]
                Red
            }
            """;

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecord.g.cs");

        Assert.NotNull(generatedSource);
        Assert.Contains("public static IEnumRecordAccessor<TEnum> Resolve<TEnum>() where TEnum : struct, global::System.Enum", generatedSource!.SourceText);
        Assert.Contains("return typeof(TEnum) switch", generatedSource.SourceText);
        Assert.Contains("{ } type when type == typeof(global::TestNamespace.Colors) => (IEnumRecordAccessor<TEnum>)(object)new ColorsAccessor(),", generatedSource.SourceText);
    }

    [Fact]
    public void GeneratesTryResolveMethod()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ColorProps(string Name);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red")]
                Red
            }
            """;

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecord.g.cs");

        Assert.NotNull(generatedSource);
        Assert.Contains("public static bool TryResolve<TEnum>([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IEnumRecordAccessor<TEnum>? accessor) where TEnum : struct, global::System.Enum", generatedSource!.SourceText);
    }

    [Fact]
    public void GeneratesRequireEnumRecordAttribute()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ColorProps(string Name);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red")]
                Red
            }
            """;

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecordAttributes.g.cs");

        Assert.NotNull(generatedSource);
        Assert.Contains("public sealed class RequireEnumRecordAttribute<TInterface> : global::System.Attribute", generatedSource!.SourceText);
        Assert.Contains("public int TypeParameterIndex { get; set; } = 0;", generatedSource.SourceText);
    }

    [Fact]
    public void MultipleEnums_GeneratesUniqueInterfaces()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ColorProps(string Name, string HexCode);
            public readonly record struct SizeProps(string Name, int Pixels);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red", "#FF0000")]
                Red
            }

            [EnumRecord<SizeProps>]
            public enum Sizes
            {
                [EnumData("Small", 10)]
                Small
            }
            """;

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecordInterfaces.g.cs");

        Assert.NotNull(generatedSource);
        // Should have IHasName (shared), IHasHexCode (Colors only), IHasPixels (Sizes only)
        Assert.Contains("public interface IHasName { }", generatedSource!.SourceText);
        Assert.Contains("public interface IHasHexCode { }", generatedSource.SourceText);
        Assert.Contains("public interface IHasPixels { }", generatedSource.SourceText);
        
        // Should only appear once
        Assert.Equal(1, CountOccurrences(generatedSource.SourceText, "public interface IHasName { }"));
    }

    [Fact]
    public void ResolveMethod_HandlesMultipleEnums()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct ColorProps(string Name);
            public readonly record struct SizeProps(string Label);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red")]
                Red
            }

            [EnumRecord<SizeProps>]
            public enum Sizes
            {
                [EnumData("Small")]
                Small
            }
            """;

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(source, "EnumRecord.g.cs");

        Assert.NotNull(generatedSource);
        Assert.Contains("{ } type when type == typeof(global::TestNamespace.Colors) => (IEnumRecordAccessor<TEnum>)(object)new ColorsAccessor(),", generatedSource!.SourceText);
        Assert.Contains("{ } type when type == typeof(global::TestNamespace.Sizes) => (IEnumRecordAccessor<TEnum>)(object)new SizesAccessor(),", generatedSource.SourceText);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
