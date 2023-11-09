using TestGenerator.Attributes;

[TestGeneratorTarget("SomeName", new[] { 11, 22, 33 }, Type = typeof(StreamReader))]
internal partial class TestTarget
{
    static void Main()
    {
        Console.WriteLine(new TestTarget().ToString());
    }
}