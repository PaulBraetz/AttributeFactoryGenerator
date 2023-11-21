namespace TestGenerator.Attributes;

using RhoMicro.AttributeFactoryGenerator;

[GenerateFactory]
partial class TestGeneratorTargetAttribute
{
    [ExcludeFromFactory]
    private TestGeneratorTargetAttribute(System.Object typeSymbolContainer) => _typeSymbolContainer = typeSymbolContainer;
    void Test()
    {
        _ = TryCreate(null, out _);
    }
}
