﻿using System.IO;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.YAML;
using YamlDotNet.RepresentationModel;
// ReSharper disable AccessToStaticMemberViaDerivedType

namespace Robust.UnitTesting.Shared.Serialization.YamlObjectSerializerTests
{
    [TestFixture]
    public class TypePropertySerialization_Test : RobustUnitTest
    {
        [Test]
        public void SerializeTypePropertiesTest()
        {
            ITestType? type = new TestTypeTwo
            {
                TestPropertyOne = "B",
                TestPropertyTwo = 10
            };
            var factory = new YamlDataNodeFactory();
            var serMan = IoCManager.Resolve<IServ3Manager>();
            var mapping = (YamlMappingDataNode) serMan.WriteValue(type, factory);

            Assert.IsNotEmpty(mapping.Children);

            var testPropertyOne = (IValueDataNode) ((IMappingDataNode) mapping["test"])["testPropertyOne"];
            var testPropertyTwo = (IValueDataNode) ((IMappingDataNode) mapping["test"])["testPropertyTwo"];

            Assert.That(testPropertyOne.GetValue(), Is.EqualTo("B"));
            Assert.That(testPropertyTwo.Value, Is.EqualTo("10"));
        }

        [Test]
        public void DeserializeTypePropertiesTest()
        {
            var yaml = @"
- test:
    !type:testtype2
    testPropertyOne: A
    testPropertyTwo: 5
";

            using var stream = new MemoryStream();

            var writer = new StreamWriter(stream);
            writer.Write(yaml);
            writer.Flush();
            stream.Position = 0;

            var streamReader = new StreamReader(stream);
            var yamlStream = new YamlStream();
            yamlStream.Load(streamReader);

            var mapping = (YamlMappingNode) yamlStream.Documents[0].RootNode[0];

            var serMan = IoCManager.Resolve<IServ3Manager>();
            var type = serMan.ReadValue<ITestType>(new YamlMappingDataNode(mapping));

            Assert.NotNull(type);
            Assert.IsInstanceOf<TestTypeTwo>(type);

            var testTypeTwo = (TestTypeTwo) type!;

            Assert.That(testTypeTwo.TestPropertyOne, Is.EqualTo("A"));
            Assert.That(testTypeTwo.TestPropertyTwo, Is.EqualTo(5));
        }
    }

    [SerializedType("testtype2")]
    [DataDefinition]
    public class TestTypeTwo : ITestType
    {
        [DataField("testPropertyOne")]
        public string? TestPropertyOne { get; set; }

        [DataField("testPropertyTwo")]
        public int TestPropertyTwo { get; set; }
    }

    [RegisterComponent]
    public class TestComponent : Component
    {
        public override string Name => "Test";

        [DataField("testType")] public ITestType? TestType { get; set; }
    }
}