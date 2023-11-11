namespace TestGenerator.Attributes;

using RhoMicro.AttributeFactoryGenerator;

[GenerateFactory]
partial class TestGeneratorTargetAttribute
{
    [ExcludeConstructor]
    private TestGeneratorTargetAttribute() { }

    void Test()
    {
        _ = TryCreate(null, out _);
    }
}
