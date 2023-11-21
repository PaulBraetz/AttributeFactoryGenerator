namespace TestGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Collections.Immutable;
using System.Linq;

using TestGenerator.Attributes;

[Generator]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.CreateSyntaxProvider(
            (n, t) => n is ClassDeclarationSyntax,
            (c, t) =>
            {
                var symbol = (ITypeSymbol)c.SemanticModel.GetDeclaredSymbol(c.Node);

                ImmutableArray<AttributeData> attributes = symbol.GetAttributes();

                IEnumerable<TestGeneratorTargetAttribute> allParsed =
                    attributes.OfTestGeneratorTargetAttribute();
                TestGeneratorTargetAttribute singleParsed =
                    TestGeneratorTargetAttribute.TryCreate(attributes[0], out var s) ? s : null;
                singleParsed = symbol.TryGetFirstTestGeneratorTargetAttribute(out var a) ? a : null;

                var attribute = symbol.GetAttributes()
                    .OfTestGeneratorTargetAttribute()
                    .Single();
                var impl = $"\"{attribute.TypeSymbol.Name}|{attribute.Name}|{String.Join("|", attribute.Ages)}\"";
                var result = $"partial class {symbol.Name}{{public override string ToString()=>{impl};}}";
                return (Hint: symbol.Name, Source: result);
            });

        context.RegisterSourceOutput(provider, (c, t) => c.AddSource(t.Hint, t.Source));
    }
}
