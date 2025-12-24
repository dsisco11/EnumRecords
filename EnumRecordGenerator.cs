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

        return new EnumInfo(
            enumSymbol.Name,
            namespaceName,
            properties,
            members);
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
                    properties.Add(new PropertyInfo(
                        param.Name,
                        GetTypeDisplayName(param.Type)));
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
";

    private sealed class EnumInfo
    {
        public string EnumName { get; }
        public string? Namespace { get; }
        public List<PropertyInfo> Properties { get; }
        public List<EnumMemberInfo> Members { get; }

        public EnumInfo(string enumName, string? ns, List<PropertyInfo> properties, List<EnumMemberInfo> members)
        {
            EnumName = enumName;
            Namespace = ns;
            Properties = properties;
            Members = members;
        }
    }

    private sealed class PropertyInfo
    {
        public string Name { get; }
        public string TypeName { get; }

        public PropertyInfo(string name, string typeName)
        {
            Name = name;
            TypeName = typeName;
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
