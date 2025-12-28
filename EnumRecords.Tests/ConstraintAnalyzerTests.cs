using EnumRecords.Tests.Helpers;
using Xunit;

namespace EnumRecords.Tests;

/// <summary>
/// Tests for the EnumRecordConstraintAnalyzer.
/// </summary>
public class ConstraintAnalyzerTests
{
    [Fact]
    public async Task ENUMREC010_ConcreteType_MissingInterface_ReportsDiagnostic()
    {
        var source = """
            using EnumRecords;
            using EnumRecords.Contracts;

            namespace TestNamespace;

            // Interface that we require
            public interface IHasHexCode { }

            // Properties struct WITHOUT IHasHexCode
            public readonly record struct ColorProps(string Name);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red")]
                Red
            }

            public static class Consumer
            {
                [RequireEnumRecord<IHasHexCode>]
                public static string GetHex<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => "test";

                public static void Test()
                {
                    // This should trigger ENUMREC010 - Colors doesn't have IHasHexCode
                    GetHex(Colors.Red);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsByIdAsync(source, "ENUMREC010");

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("Colors"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("IHasHexCode"));
    }

    [Fact]
    public async Task ENUMREC010_ConcreteType_HasInterface_NoDiagnostic()
    {
        var source = """
            using EnumRecords;
            using EnumRecords.Contracts;

            namespace TestNamespace;

            // Interface that we require
            public interface IHasHexCode { }

            // Properties struct WITH IHasHexCode
            public readonly record struct ColorProps(string Name, string HexCode) : IHasHexCode;

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red", "#FF0000")]
                Red
            }

            public static class Consumer
            {
                [RequireEnumRecord<IHasHexCode>]
                public static string GetHex<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => "test";

                public static void Test()
                {
                    // This should pass - Colors has ColorProps which implements IHasHexCode
                    GetHex(Colors.Red);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsByIdAsync(source, "ENUMREC010");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ENUMREC011_TypeParameter_MissingPropagation_ReportsDiagnostic()
    {
        var source = """
            using EnumRecords;
            using EnumRecords.Contracts;

            namespace TestNamespace;

            public interface IHasHexCode { }

            public static class Consumer
            {
                [RequireEnumRecord<IHasHexCode>]
                public static string Inner<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => "test";

                // Missing [RequireEnumRecord<IHasHexCode>] - should trigger ENUMREC011
                public static string Outer<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => Inner(value);
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsByIdAsync(source, "ENUMREC011");

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("Outer"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("Inner"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("IHasHexCode"));
    }

    [Fact]
    public async Task ENUMREC011_TypeParameter_HasPropagation_NoDiagnostic()
    {
        var source = """
            using EnumRecords;
            using EnumRecords.Contracts;

            namespace TestNamespace;

            public interface IHasHexCode { }

            public static class Consumer
            {
                [RequireEnumRecord<IHasHexCode>]
                public static string Inner<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => "test";

                [RequireEnumRecord<IHasHexCode>]  // Constraint is propagated
                public static string Outer<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => Inner(value);
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsByIdAsync(source, "ENUMREC011");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ENUMREC012_ConcreteType_NoEnumRecord_ReportsDiagnostic()
    {
        var source = """
            using EnumRecords;
            using EnumRecords.Contracts;

            namespace TestNamespace;

            public interface IHasName { }

            // Enum WITHOUT [EnumRecord<T>]
            public enum SimpleEnum
            {
                Value1,
                Value2
            }

            public static class Consumer
            {
                [RequireEnumRecord<IHasName>]
                public static string GetName<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => "test";

                public static void Test()
                {
                    // This should trigger ENUMREC012 - SimpleEnum has no EnumRecord at all
                    GetName(SimpleEnum.Value1);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsByIdAsync(source, "ENUMREC012");

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("SimpleEnum"));
    }

    [Fact]
    public async Task ENUMREC011_StricterConstraint_NoDiagnostic()
    {
        var source = """
            using EnumRecords;
            using EnumRecords.Contracts;

            namespace TestNamespace;

            public interface IHasName { }
            public interface IHasNameAndCode : IHasName { }

            public static class Consumer
            {
                [RequireEnumRecord<IHasName>]
                public static string Inner<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => "test";

                // Has stricter constraint (IHasNameAndCode : IHasName), should satisfy Inner's requirement
                [RequireEnumRecord<IHasNameAndCode>]
                public static string Outer<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => Inner(value);
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsByIdAsync(source, "ENUMREC011");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MultipleConstraints_AllMustBeSatisfied()
    {
        var source = """
            using EnumRecords;
            using EnumRecords.Contracts;

            namespace TestNamespace;

            public interface IHasName { }
            public interface IHasCode { }

            public readonly record struct Props(string Name) : IHasName;

            [EnumRecord<Props>]
            public enum TestEnum
            {
                [EnumData("Test")]
                Value
            }

            public static class Consumer
            {
                [RequireEnumRecord<IHasName>]
                [RequireEnumRecord<IHasCode>]  // Both required
                public static string GetBoth<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => "test";

                public static void Test()
                {
                    // Should fail on IHasCode (Props only implements IHasName)
                    GetBoth(TestEnum.Value);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsByIdAsync(source, "ENUMREC010");

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("IHasCode"));
    }

    [Fact]
    public async Task ClassLevelConstraint_AppliestoMethods()
    {
        var source = """
            using EnumRecords;
            using EnumRecords.Contracts;

            namespace TestNamespace;

            public interface IHasHexCode { }

            public readonly record struct ColorProps(string Name);

            [EnumRecord<ColorProps>]
            public enum Colors
            {
                [EnumData("Red")]
                Red
            }

            [RequireEnumRecord<IHasHexCode>]  // Class-level constraint
            public static class ConstrainedClass
            {
                public static string GetHex<TEnum>(TEnum value) where TEnum : struct, System.Enum
                    => "test";
            }

            public static class Consumer
            {
                public static void Test()
                {
                    // Should trigger diagnostic - class-level constraint applies
                    ConstrainedClass.GetHex(Colors.Red);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsByIdAsync(source, "ENUMREC010");

        Assert.NotEmpty(diagnostics);
    }
}
