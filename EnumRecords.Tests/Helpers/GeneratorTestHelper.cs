using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using EnumRecords;

namespace EnumRecords.Tests.Helpers;

/// <summary>
/// Helper class for running the EnumRecordGenerator in tests.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Runs the EnumRecordGenerator on the provided source code and returns the result.
    /// </summary>
    public static GeneratorRunResult RunGenerator(string source)
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

        var generator = new EnumRecordGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator).WithUpdatedParseOptions(parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();

        return new GeneratorRunResult(
            OutputCompilation: outputCompilation,
            Diagnostics: diagnostics,
            GeneratedSources: runResult.GeneratedTrees.Select(t => new GeneratedSource(
                HintName: Path.GetFileName(t.FilePath),
                SourceText: t.GetText().ToString()
            )).ToImmutableArray(),
            GeneratorDiagnostics: runResult.Results.SelectMany(r => r.Diagnostics).ToImmutableArray()
        );
    }

    /// <summary>
    /// Runs the generator and returns only the generated source files (excluding attributes).
    /// </summary>
    public static ImmutableArray<GeneratedSource> GetGeneratedSources(string source, bool includeAttributes = false)
    {
        var result = RunGenerator(source);
        
        if (includeAttributes)
            return result.GeneratedSources;
        
        return result.GeneratedSources
            .Where(s => !s.HintName.Contains("Attributes"))
            .ToImmutableArray();
    }

    /// <summary>
    /// Gets the generated source file with the specified hint name.
    /// </summary>
    public static GeneratedSource? GetGeneratedSource(string source, string hintName)
    {
        var result = RunGenerator(source);
        return result.GeneratedSources.FirstOrDefault(s => s.HintName == hintName);
    }

    /// <summary>
    /// Gets all diagnostics (both compilation and generator) from running the generator.
    /// </summary>
    public static ImmutableArray<Diagnostic> GetAllDiagnostics(string source)
    {
        var result = RunGenerator(source);
        return result.Diagnostics.AddRange(result.GeneratorDiagnostics);
    }

    /// <summary>
    /// Gets only generator-produced diagnostics with the specified ID.
    /// </summary>
    public static ImmutableArray<Diagnostic> GetDiagnosticsById(string source, string diagnosticId)
    {
        var result = RunGenerator(source);
        return result.GeneratorDiagnostics
            .Where(d => d.Id == diagnosticId)
            .ToImmutableArray();
    }

    /// <summary>
    /// Verifies the output compilation has no errors (excluding generator diagnostics).
    /// </summary>
    public static bool CompilationSucceeds(string source)
    {
        var result = RunGenerator(source);
        return !result.OutputCompilation.GetDiagnostics()
            .Any(d => d.Severity == DiagnosticSeverity.Error);
    }
}

/// <summary>
/// Result from running the source generator.
/// </summary>
public record GeneratorRunResult(
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<GeneratedSource> GeneratedSources,
    ImmutableArray<Diagnostic> GeneratorDiagnostics
);

/// <summary>
/// Represents a generated source file.
/// </summary>
public record GeneratedSource(string HintName, string SourceText);
