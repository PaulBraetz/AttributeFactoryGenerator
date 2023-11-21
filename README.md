# RhoMicro.AttributeFactoryGenerator

Are you creating source generators for C#? Are you using attributes to allow your consumers to instruct your generator? Are you dissatisfied with the amount of boilerplate you have to write in order to extract those instructions from the roslyn api? 

Then this project could be of use to you!

## Key Features & Limitations
- Generate Factory for parsing attribute instance from `AttributeData`
- Generate helper functions for retrieving instances of your attribute from an `IEnumerable<AttributeData>`
- Parse type properties as `ITypeSymbol`s

- The attribute type (or semantically equivalent type) has to be available to the generator

## How to use

In the assembly through which I distribute the attributes, I declare one:
```cs
[AttributeUsage(AttributeTargets.Class)]
public partial class TestGeneratorTargetAttribute : Attribute
{
    public TestGeneratorTargetAttribute(String name, Int32[] ages)
    {
        Name = name;
        Ages = ages;
    }
    public TestGeneratorTargetAttribute(Type type) => Type = type;

    public String Name { get; }
    public Int32[] Ages { get; }
    public Type? Type { get; set; }
}
```

Then, in the generator assembly, I add a link to the source file above, as well as a second partial declaration like so:
```cs
[GenerateFactory]
partial class TestGeneratorTargetAttribute
{
    [ExcludeFromFactory]
    private TestGeneratorTargetAttribute(System.Object typeSymbolContainer) =>
        _typeSymbolContainer = typeSymbolContainer;
}
```
The constructor above is required in order to construct an instance when the consumer made use of a constructor taking at least one parameter of type `Type`.

For every constructor that takes at least one parameter of type `Type`, an equivalent factory constructor is required. These are expected to take an instance of `Object` instead of the type and assign it to the generated helper field.

This way, a generated helper property of type `ITypeSymbol` may be used to retrieve the type used by the consumer in their `typeof` expression.


Use the generated factory and helper methods like so:
```cs
ImmutableArray<AttributeData> attributes = 
    symbol.GetAttributes();

IEnumerable<TestGeneratorTargetAttribute> allParsed =
    attributes.OfTestGeneratorTargetAttribute();

TestGeneratorTargetAttribute singleParsed =
    TestGeneratorTargetAttribute.TryCreate(attributes[0], out var a) ? 
    a : 
    null;

singleParsed = 
    symbol.TryGetFirstTestGeneratorTargetAttribute(out var a) ? 
    a : 
    null;
```
The generated extension method `OfTestGeneratorTargetAttribute` will return all instances of `TestGeneratorTargetAttribute` found in the symbols list of attributes.

The generated extension method `TryGetFirstTestGeneratorTargetAttribute` attempts to retrieve the first instance of `TestGeneratorTargetAttribute` found on the symbol.