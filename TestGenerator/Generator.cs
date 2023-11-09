namespace TestGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
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
                var attribute = symbol.GetAttributes()
                    .Select(a => (Success: TestGeneratorTargetAttribute.TryCreate(a, out var r), Instance: r))
                    .Where(t => t.Success)
                    .Select(t => t.Instance)
                    .Single();
                var impl = $"\"{attribute.TypeSymbol.Name}|{attribute.Name}|{String.Join("|", attribute.Ages)}\"";
                var result = $"partial class {symbol.Name}{{public override string ToString()=>{impl};}}";
                return (Hint: symbol.Name, Source: result);
            });

        context.RegisterSourceOutput(provider, (c, t) => c.AddSource(t.Hint, t.Source));
    }
}
