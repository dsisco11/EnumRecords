using EnumRecords.Tests.Helpers;
using Xunit;

namespace EnumRecords.Tests;

/// <summary>
/// Tests for diagnostic reporting from the source generator.
/// </summary>
public class DiagnosticTests
{
    [Fact]
    public void ENUMREC001_DuplicateReverseLookupValue_ReportsDiagnostic()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props([ReverseLookup] string Code);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("SAME")]
                First,
                
                [EnumData("SAME")]
                Second
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnosticsById(source, "ENUMREC001");

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("SAME"));
    }

    [Fact]
    public void ENUMREC002_MissingEnumDataAttribute_ReportsDiagnostic()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("Has Data")]
                First,
                
                // Missing [EnumData] attribute
                Second
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnosticsById(source, "ENUMREC002");

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("Second"));
    }

    [Fact]
    public void ENUMREC003_WrongArgumentCount_TooFew_ReportsDiagnostic()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name, int Code);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("Only one arg")]  // Missing second argument
                First
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnosticsById(source, "ENUMREC003");

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("1 argument"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("expects 2"));
    }

    [Fact]
    public void ENUMREC003_WrongArgumentCount_TooMany_ReportsDiagnostic()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("Name", "Extra", "Arguments")]  // Too many arguments
                First
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnosticsById(source, "ENUMREC003");

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("3 argument"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("expects 1"));
    }

    [Fact]
    public void NoDiagnostics_WhenEnumIsValid()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props(string Name, int Code);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("First", 1)]
                First,
                
                [EnumData("Second", 2)]
                Second
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generatorDiagnostics = result.GeneratorDiagnostics;

        Assert.Empty(generatorDiagnostics);
    }

    [Fact]
    public void ENUMREC001_CaseSensitive_DuplicatesDetected()
    {
        // Without CaseInsensitive, "abc" and "ABC" should be different (no error)
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props([ReverseLookup] string Code);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("abc")]
                Lower,
                
                [EnumData("ABC")]
                Upper
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnosticsById(source, "ENUMREC001");

        // Case-sensitive by default, so these should NOT be duplicates
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ENUMREC001_CaseInsensitive_DuplicatesDetected()
    {
        // With CaseInsensitive = true, exact same lowercase values should be considered duplicates
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props([ReverseLookup(CaseInsensitive = true)] string Code);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("same")]
                First,
                
                [EnumData("same")]
                Second
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnosticsById(source, "ENUMREC001");

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void MultipleDiagnostics_AllReported()
    {
        var source = """
            using EnumRecords;

            namespace TestNamespace;

            public readonly record struct Props([ReverseLookup] string Code, int Value);

            [EnumRecord<Props>]
            public enum Status
            {
                [EnumData("DUPE", 1)]
                First,
                
                [EnumData("DUPE", 2)]
                Second,
                
                // Missing EnumData
                Third,
                
                [EnumData("Only one")]  // Wrong arg count
                Fourth
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        // Should have ENUMREC001 (duplicate), ENUMREC002 (missing), and ENUMREC003 (wrong count)
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ENUMREC001");
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ENUMREC002");
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ENUMREC003");
    }
}
