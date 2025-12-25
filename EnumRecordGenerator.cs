using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EnumRecords;

[Generator]
public class EnumRecordGenerator : IIncrementalGenerator
{
    private const string EnumRecordAttributeName = "EnumRecordAttribute";
    private const string EnumRecordPropertiesAttributeName = "EnumRecordPropertiesAttribute";
    private const string ReverseLookupAttributeName = "ReverseLookupAttribute";

    private static readonly DiagnosticDescriptor DuplicateReverseLookupValue = new(
        id: "ENUMREC001",
        title: "Duplicate reverse-lookup value",
        messageFormat: "Duplicate value '{0}' for reverse-lookup property '{1}' on enum '{2}'",
        category: "EnumRecords",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source code
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource("EnumRecordAttributes.g.cs", SourceText.From(AttributeSource, Encoding.UTF8));
        });

        // Find all enums with [EnumRecord<T>] attribute
        var enumDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "EnumRecords.EnumRecordAttribute`1",
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (ctx, _) => GetEnumInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        // Generate extension methods
        context.RegisterSourceOutput(enumDeclarations, static (ctx, enumInfo) =>
        {
            // Report diagnostics for duplicate reverse-lookup values
            foreach (var diagnostic in enumInfo.Diagnostics)
            {
                ctx.ReportDiagnostic(diagnostic);
            }

            var source = GenerateExtensionMethods(enumInfo);
            ctx.AddSource($"{enumInfo.EnumName}Extensions.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static EnumInfo? GetEnumInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol enumSymbol)
            return null;

        var enumDeclaration = (EnumDeclarationSyntax)context.TargetNode;

        // Get the EnumRecord attribute and extract the type argument
        var enumRecordAttribute = context.Attributes
            .FirstOrDefault(a => a.AttributeClass?.Name == EnumRecordAttributeName);

        if (enumRecordAttribute?.AttributeClass is not INamedTypeSymbol { TypeArguments.Length: 1 } attrClass)
            return null;

        var propertiesType = attrClass.TypeArguments[0];
        
        // Get the properties from the record struct
        var properties = GetRecordProperties(propertiesType);
        if (properties.Count == 0)
            return null;

        // Get enum members with their property values
        var members = new List<EnumMemberInfo>();
        foreach (var member in enumSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (!member.HasConstantValue)
                continue;

            var memberSyntax = member.DeclaringSyntaxReferences
                .FirstOrDefault()?.GetSyntax() as EnumMemberDeclarationSyntax;

            if (memberSyntax == null)
                continue;

            var propsAttribute = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == EnumRecordPropertiesAttributeName);

            if (propsAttribute == null)
                continue;

            // The params array comes as a single argument containing an array of values
            var values = new List<string>();
            if (propsAttribute.ConstructorArguments.Length == 1 &&
                propsAttribute.ConstructorArguments[0].Kind == TypedConstantKind.Array)
            {
                var arrayValues = propsAttribute.ConstructorArguments[0].Values;
                values = arrayValues.Select(v => GetTypedConstantValue(v)).ToList();
            }
            else
            {
                values = propsAttribute.ConstructorArguments
                    .Select(arg => GetTypedConstantValue(arg))
                    .ToList();
            }

            if (values.Count != properties.Count)
                continue;

            members.Add(new EnumMemberInfo(member.Name, values));
        }

        if (members.Count == 0)
            return null;

        var namespaceName = enumSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : enumSymbol.ContainingNamespace.ToDisplayString();

        // Validate uniqueness for reverse-lookup properties
        var diagnostics = new List<Diagnostic>();
        for (int i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            if (!property.HasReverseLookup)
                continue;

            var valueToMembers = new Dictionary<string, List<string>>();
            foreach (var member in members)
            {
                var value = member.Values[i];
                if (!valueToMembers.TryGetValue(value, out var memberList))
                {
                    memberList = new List<string>();
                    valueToMembers[value] = memberList;
                }
                memberList.Add(member.Name);
            }

            foreach (var kvp in valueToMembers)
            {
                if (kvp.Value.Count > 1)
                {
                    var diagnostic = Diagnostic.Create(
                        DuplicateReverseLookupValue,
                        enumDeclaration.Identifier.GetLocation(),
                        kvp.Key,
                        property.Name,
                        enumSymbol.Name);
                    diagnostics.Add(diagnostic);
                }
            }
        }

        return new EnumInfo(
            enumSymbol.Name,
            namespaceName,
            properties,
            members,
            diagnostics);
    }

    private static List<PropertyInfo> GetRecordProperties(ITypeSymbol propertiesType)
    {
        var properties = new List<PropertyInfo>();

        // For record structs, the primary constructor parameters become properties
        // We look for the primary constructor and its parameters
        if (propertiesType is INamedTypeSymbol namedType)
        {
            // Try to find the primary constructor (the one with parameters matching properties)
            var constructor = namedType.Constructors
                .Where(c => !c.IsImplicitlyDeclared || c.Parameters.Length > 0)
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault();

            if (constructor != null)
            {
                foreach (var param in constructor.Parameters)
                {
                    var hasReverseLookup = param.GetAttributes()
                        .Any(a => a.AttributeClass?.Name == ReverseLookupAttributeName);

                    properties.Add(new PropertyInfo(
                        param.Name,
                        GetTypeDisplayName(param.Type),
                        hasReverseLookup));
                }
            }
        }

        return properties;
    }

    private static string GetTypeDisplayName(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Byte => "byte",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_Char => "char",
            SpecialType.System_String => "string",
            _ => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        };
    }

    private static string GetTypedConstantValue(TypedConstant constant)
    {
        if (constant.IsNull)
            return "null";

        return constant.Type?.SpecialType switch
        {
            SpecialType.System_String => $"\"{EscapeString(constant.Value?.ToString() ?? "")}\"",
            SpecialType.System_Char => $"'{constant.Value}'",
            SpecialType.System_Boolean => constant.Value?.ToString()?.ToLowerInvariant() ?? "false",
            SpecialType.System_Single => $"{constant.Value}f",
            SpecialType.System_Double => $"{constant.Value}d",
            SpecialType.System_Decimal => $"{constant.Value}m",
            SpecialType.System_Int64 => $"{constant.Value}L",
            SpecialType.System_UInt64 => $"{constant.Value}UL",
            SpecialType.System_UInt32 => $"{constant.Value}U",
            _ => constant.Value?.ToString() ?? "default"
        };
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string GenerateExtensionMethods(EnumInfo enumInfo)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (enumInfo.Namespace != null)
        {
            sb.AppendLine($"namespace {enumInfo.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"public static class {enumInfo.EnumName}Extensions");
        sb.AppendLine("{");

        for (int i = 0; i < enumInfo.Properties.Count; i++)
        {
            var property = enumInfo.Properties[i];
            
            if (i > 0)
                sb.AppendLine();

            sb.AppendLine($"    public static {property.TypeName} {property.Name}(this {enumInfo.EnumName} value) => value switch");
            sb.AppendLine("    {");

            foreach (var member in enumInfo.Members)
            {
                sb.AppendLine($"        {enumInfo.EnumName}.{member.Name} => {member.Values[i]},");
            }

            sb.AppendLine($"        _ => throw new global::System.ArgumentOutOfRangeException(nameof(value), value, null)");
            sb.AppendLine("    };");
        }

        // Generate reverse-lookup methods for properties marked with [ReverseLookup]
        for (int i = 0; i < enumInfo.Properties.Count; i++)
        {
            var property = enumInfo.Properties[i];
            if (!property.HasReverseLookup)
                continue;

            // Generate TryFrom method
            sb.AppendLine();
            sb.AppendLine($"    public static bool TryFrom{property.Name}({property.TypeName} value, out {enumInfo.EnumName} result)");
            sb.AppendLine("    {");
            sb.AppendLine("        (result, var success) = value switch");
            sb.AppendLine("        {");

            foreach (var member in enumInfo.Members)
            {
                sb.AppendLine($"            {member.Values[i]} => ({enumInfo.EnumName}.{member.Name}, true),");
            }

            sb.AppendLine($"            _ => (default({enumInfo.EnumName}), false)");
            sb.AppendLine("        };");
            sb.AppendLine("        return success;");
            sb.AppendLine("    }");

            // Generate From method (throwing variant)
            sb.AppendLine();
            sb.AppendLine($"    public static {enumInfo.EnumName} From{property.Name}({property.TypeName} value) => value switch");
            sb.AppendLine("    {");

            foreach (var member in enumInfo.Members)
            {
                sb.AppendLine($"        {member.Values[i]} => {enumInfo.EnumName}.{member.Name},");
            }

            sb.AppendLine($"        _ => throw new global::System.ArgumentException($\"No {enumInfo.EnumName} found with {property.Name} '{{value}}'\", nameof(value))");
            sb.AppendLine("    };");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private const string AttributeSource = @"// <auto-generated />
#nullable enable

namespace EnumRecords;

/// <summary>
/// Marks an enum as having associated record properties.
/// The type parameter specifies the record struct that defines the property schema.
/// </summary>
/// <typeparam name=""TProperties"">The record struct type defining the properties.</typeparam>
[global::System.AttributeUsage(global::System.AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class EnumRecordAttribute<TProperties> : global::System.Attribute
    where TProperties : struct
{
}

/// <summary>
/// Specifies the property values for an enum member.
/// The constructor arguments should match the order of the properties record struct's constructor parameters.
/// </summary>
[global::System.AttributeUsage(global::System.AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class EnumRecordPropertiesAttribute : global::System.Attribute
{
    public object?[] Values { get; }

    public EnumRecordPropertiesAttribute(params object?[] values)
    {
        Values = values;
    }
}

/// <summary>
/// Marks a property in the record struct as supporting reverse lookup.
/// When applied, the generator will create TryFrom{PropertyName} and From{PropertyName} methods
/// that can find the enum value by the property value.
/// Each enum member must have a unique value for this property.
/// </summary>
[global::System.AttributeUsage(global::System.AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class ReverseLookupAttribute : global::System.Attribute
{
}
";

    private sealed class EnumInfo
    {
        public string EnumName { get; }
        public string? Namespace { get; }
        public List<PropertyInfo> Properties { get; }
        public List<EnumMemberInfo> Members { get; }
        public List<Diagnostic> Diagnostics { get; }

        public EnumInfo(string enumName, string? ns, List<PropertyInfo> properties, List<EnumMemberInfo> members, List<Diagnostic> diagnostics)
        {
            EnumName = enumName;
            Namespace = ns;
            Properties = properties;
            Members = members;
            Diagnostics = diagnostics;
        }
    }

    private sealed class PropertyInfo
    {
        public string Name { get; }
        public string TypeName { get; }
        public bool HasReverseLookup { get; }

        public PropertyInfo(string name, string typeName, bool hasReverseLookup)
        {
            Name = name;
            TypeName = typeName;
            HasReverseLookup = hasReverseLookup;
        }
    }

    private sealed class EnumMemberInfo
    {
        public string Name { get; }
        public List<string> Values { get; }

        public EnumMemberInfo(string name, List<string> values)
        {
            Name = name;
            Values = values;
        }
    }
}
