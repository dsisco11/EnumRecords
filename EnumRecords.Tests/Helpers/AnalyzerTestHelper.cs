using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using EnumRecords;
using EnumRecords.Analyzers;

namespace EnumRecords.Tests.Helpers;

/// <summary>
/// Helper class for running the EnumRecordConstraintAnalyzer in tests.
/// </summary>
public static class AnalyzerTestHelper
{
    /// <summary>
    /// Runs both the generator and analyzer on the provided source code.
    /// </summary>
    public static async Task<AnalyzerRunResult> RunAnalyzerAsync(string source)
    {
        // Parse with C# preview to support generic attributes
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // First run the generator to get the generated code
        var generator = new EnumRecordGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator).WithUpdatedParseOptions(parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        // Now run the analyzer on the output compilation
        var analyzer = new EnumRecordConstraintAnalyzer();
        var compilationWithAnalyzers = outputCompilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var analyzerDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        return new AnalyzerRunResult(
            OutputCompilation: outputCompilation,
            GeneratorDiagnostics: generatorDiagnostics,
            AnalyzerDiagnostics: analyzerDiagnostics
        );
    }

    /// <summary>
    /// Gets analyzer diagnostics with the specified ID.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsByIdAsync(string source, string diagnosticId)
    {
        var result = await RunAnalyzerAsync(source);
        return result.AnalyzerDiagnostics
            .Where(d => d.Id == diagnosticId)
            .ToImmutableArray();
    }

    /// <summary>
    /// Checks if the source compiles without analyzer errors.
    /// </summary>
    public static async Task<bool> AnalyzerPassesAsync(string source)
    {
        var result = await RunAnalyzerAsync(source);
        return !result.AnalyzerDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    }
}

/// <summary>
/// Result from running the analyzer.
/// </summary>
public record AnalyzerRunResult(
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> AnalyzerDiagnostics
);
