#pragma warning disable

namespace TestGenerator.Lib
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    [GenerateFactory]
    public partial class TestGeneratorTargetAttribute : Attribute
    {
        public TestGeneratorTargetAttribute(String name, Int32[] ages)
        {
            Name = name;
            Ages = ages;
        }

        public String Name { get; }
        public Int32[] Ages { get; }
        public Type Type { get; set; }
    }
}
