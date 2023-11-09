namespace RhoMicro.AttributeFactoryGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Xml.Linq;

[Generator(LanguageNames.CSharp)]
public sealed class AttributeFactoryGenerator : IIncrementalGenerator
{
    private const String _sourceTemplate =
"""
// <generated>
// This file has been auto generated using the RhoMicro.AttributeFactoryGenerator.
// </generated>

#nullable enable
#pragma warning disable
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
namespace {NAMESPACE}
{
    {ACCESSIBILITY} partial class {NAME}
    {
        {CONTAINERS}

        {SYMBOLS}

        public static Boolean TryCreate(AttributeData data, out {NAME}? result)
        {
            result = null;
            var ctorArgs = data.ConstructorArguments;
            
            switch(ctorArgs.Length)
            {
                {CTORCASES}
            }

            var propArgs = data.NamedArguments;
            foreach(var propArg in propArgs)
            {
                switch(propArg.Key)
                {
                    {PROPCASES}
                    default:
                        return false;
                }
            }

            return true;

            static Object[] getValues(TypedConstant constant) =>
                constant.Value != null ?
                    new Object[] { constant.Value } :
                    constant.IsNull ?
                    null :
                    constant.Values.Select(getValues).ToArray();
        }
    }

    {ACCESSIBILITY} static class {NAME}Extensions
    {
        public static IEnumerable<{NAME}> Of{NAME}(this IEnumerable<AttributeData> data) =>
            data.Select(d => (Success: {NAME}.TryCreate(d, out var a), Attribute:a))
                .Where(t=>t.Success)
                .Select(t=>t.Attribute);
    }
}
""";
    private const String _extensionsPlaceholder = "{EXTENSIONS}";
    private const String _targetCtorCasesPlaceholder = "{CTORCASES}";
    private const String _targetPropCasesPlaceholder = "{PROPCASES}";
    private const String _targetNamePlaceholder = "{NAME}";
    private const String _targetContainersPlaceholder = "{CONTAINERS}";
    private const String _targetSymbolsPlaceholder = "{SYMBOLS}";
    private const String _targetAccessibilityPlaceholder = "{ACCESSIBILITY}";
    private const String _targetNamespacePlaceholder = "{NAMESPACE}";

    private const String _attributeName = "GenerateFactoryAttribute";
    private const String _attributeNamespace = "RhoMicro.AttributeFactoryGenerator";
    private const String _attributeSource =
"""
// <generated>
// This file has been auto generated using the RhoMicro.AttributeFactoryGenerator.
// </generated>
#pragma warning disable
using System;
namespace RhoMicro.AttributeFactoryGenerator
{
    /// <summary>
    /// Marks the target type for factory generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class GenerateFactoryAttribute : Attribute { }
}
""";

    //source: https://stackoverflow.com/a/58853591
    private static readonly Regex _camelCasePattern = new(@"([A-Z])([A-Z]+|[a-z0-9_]+)($|[A-Z]\w*)", RegexOptions.Compiled);

    private static String ToCamelCase(String name)
    {
        if(name.Length == 0)
        {
            return name;
        }

        if(name.Length == 1 && Char.IsLower(name[0]))
        {
            return name;
        }

        //source: https://stackoverflow.com/a/58853591
        var result = _camelCasePattern.Replace(
            name,
            m => m.Groups[1].Value.ToLower() + m.Groups[2].Value.ToLower() + m.Groups[3].Value);

        return result;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetSet = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        var provider = context.SyntaxProvider.CreateSyntaxProvider(
            static (n, t) => n is TypeDeclarationSyntax,
            static (c, t) =>
                (c.Node as TypeDeclarationSyntax).Modifiers.Any(SyntaxKind.PartialKeyword) &&
                c.SemanticModel.GetDeclaredSymbol(c.Node, cancellationToken: t)
                    .GetAttributes()
                    //.Select(a => (Success: MyAttribute.TryCreate(a, out var r), Result: r))
                    //.Where(t => t.Success)
                    //.Select(t => t.Result)
                    //.ToArray()
                    .Select(a => a.AttributeClass)
                    .Any(c => c.Name == _attributeName &&
                        c.ContainingNamespace.ToDisplayString() == _attributeNamespace) ?
                (Success: true, Target: c.Node as TypeDeclarationSyntax, c.SemanticModel) :
                default)
            .Where(static t => t.Success)
            .Select(static (d, t) =>
            {
                var (_, target, semanticModel) = d;
                var symbol = semanticModel.GetDeclaredSymbol(target, cancellationToken: t);
                return (d.Target, d.SemanticModel, Symbol: symbol);
            })
            .Where(t => targetSet.Add(t.Symbol))
            .Select(static (c, t) =>
            {
                var typeProps = c.Symbol.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Type")
                    .ToArray();

                var containerFields = typeProps
                    .Select(p => ToCamelCase(p.Name))
                    .Select(n => $"private Object _{n}SymbolContainer;");

                var symbolProps = typeProps
                    .Select(p =>
$"\t\tpublic ITypeSymbol {p.Name}Symbol{{" +
$"get => (ITypeSymbol)_{ToCamelCase(p.Name)}SymbolContainer;" +
$"set => _{ToCamelCase(p.Name)}SymbolContainer = value;}}");

                var propCases = c.Symbol.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p => p.SetMethod != null && p.SetMethod.DeclaredAccessibility == Accessibility.Public)
                    .Select(p => (
                        Type: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        p.Name))
                    .Select(t =>
                        (t.Type,
                        t.Name,
                        IsArray: t.Type.EndsWith("[]"),
                        IsObjectArray: t.Type == "object[]",
                        IsType: t.Type == "global::System.Type"))
                    .Select(t =>
                        (t.Name,
                        t.IsType,
                        Expression: t.IsArray ?
                            (t.IsObjectArray ?
                            //object array
                            $"getValues(propArg.Value)" :
                            //regular array
                            $"propArg.Value.Values.Select(c => ({t.Type.Substring(0, t.Type.Length - 2)})c.Value).ToArray()") :
                            //scalar
                            $"{(t.IsType || t.Type == "object" ? String.Empty : $"({t.Type})")}propArg.Value.Value"))
                    .Select(t => $"case \"{t.Name}\":result.{(t.IsType ? $"_{ToCamelCase(t.Name)}SymbolContainer" : t.Name)} = {t.Expression};break;");

                var source = _sourceTemplate
                    .Replace(_targetPropCasesPlaceholder, String.Join("\n", propCases))
                    .Replace(_targetSymbolsPlaceholder, String.Join("\n", symbolProps))
                    .Replace(_targetContainersPlaceholder, String.Join("\n", containerFields));

                return (c.Symbol, Source: source);
            })
            .Select(static (c, t) =>
            {
                var constructors = c.Symbol.Constructors;

                var ctorCases = constructors.GroupBy(c => c.Parameters.Length)
                    .Select(g =>
                    {
                        var groupBranches = g.Select(c =>
                        {
                            var parameters = c.Parameters
                                .Select(p => (
                                    Type: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                    p.Name))
                                .Select(t =>
                                    (t.Type,
                                    t.Name,
                                    IsArray: t.Type.EndsWith("[]"),
                                    IsObjectArray: t.Type == "object[]",
                                    IsType: t.Type == "global::System.Type"))
                                .ToArray();

                            var conditions = String.Join(
                                "&&",
                                parameters.Select((p, i) =>
                                    $"ctorArgs[{i}].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == \"{p.Type}\""));

                            var body = String.Concat(
                                parameters.Select((p, i) =>
                                {
                                    var argDeclaration = p.IsArray ?
                                    (p.IsObjectArray ?
                                     //object array
                                     $"var arg{i} = getValues(ctorArgs[{i}]);" :
                                     //regular array
                                     $"var arg{i} = ctorArgs[{i}].Values.Select(c => ({p.Type.Substring(0, p.Type.Length - 2)})c.Value).ToArray();") :
                                     //scalar
                                     $"var arg{i} = {(p.IsType || p.Type == "object" ? String.Empty : $"({p.Type})")}ctorArgs[{i}].Value;";

                                    return argDeclaration;
                                }));
                            var args = String.Join(",", parameters.Select((p, i) => $"{(p.IsType ? $"{p.Name}SymbolContainer" : p.Name)}:arg{i}"));
                            var result = $"{(c.Parameters.Length > 0 ? $"if({conditions}){{" : String.Empty)}{body}result = new {{NAME}}({args});{(c.Parameters.Length > 0 ? "}" : String.Empty)}";

                            return result;
                        }).ToList();
                        if(g.Key != 0)
                        {
                            groupBranches.Add("{return false;}break;");
                        } else
                        {
                            groupBranches[0] = groupBranches[0] + "break;";
                        }

                        var groupBranchesSource = $"case {g.Key}:{String.Join("else ", groupBranches)}";

                        return groupBranchesSource;
                    }).Append("default:return false;");

                var ctorCasesSource = String.Concat(ctorCases);

                var source = c.Source.Replace(_targetCtorCasesPlaceholder, ctorCasesSource);

                return (c.Symbol, Source: source);
            })
            .Select(static (c, t) =>
            {
                var source = c.Source
                    .Replace(_targetNamePlaceholder, c.Symbol.Name)
                    .Replace(_targetAccessibilityPlaceholder, SyntaxFacts.GetText(c.Symbol.DeclaredAccessibility))
                    .Replace(_targetNamespacePlaceholder, c.Symbol.ContainingNamespace.ToDisplayString());

                return (c.Symbol, Source: source);
            })
            .Select(static (c, t) =>
            {
                var (symbol, source) = c;

                var hint = $"{symbol.Name}_Factory.cs";

                //source: https://stackoverflow.com/a/74412674
                source = CSharpSyntaxTree.ParseText(source, cancellationToken: t)
                    .GetRoot(t)
                    .NormalizeWhitespace()
                    .SyntaxTree
                    .GetText(t)
                    .ToString();

                return (Hint: hint, Source: source);
            });

        context.RegisterPostInitializationOutput(static c => c.AddSource(_attributeName, _attributeSource));
        context.RegisterSourceOutput(provider, static (c, s) => c.AddSource(s.Hint, s.Source));
    }
}
