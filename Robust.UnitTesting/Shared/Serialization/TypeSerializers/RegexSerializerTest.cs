﻿using System.Text.RegularExpressions;
using NUnit.Framework;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.TypeSerializers;

namespace Robust.UnitTesting.Shared.Serialization.TypeSerializers
{
    [TestFixture]
    [TestOf(typeof(RegexSerializer))]
    public class RegexSerializerTest : TypeSerializerTest
    {
        [Test]
        public void SerializationTest()
        {
            var str = "[AEIOU]";
            var regex = new Regex(str);
            var node = (ValueDataNode) Serialization.WriteValue(regex);

            Assert.That(node.Value, Is.EqualTo(str));
        }

        [Test]
        public void DeserializationTest()
        {
            var str = "[AEIOU]";
            var node = new ValueDataNode(str);
            var deserializedRegex = Serialization.ReadValueOrThrow<Regex>(node);
            var regex = new Regex(str, RegexOptions.Compiled);

            Assert.That(deserializedRegex.ToString(), Is.EqualTo(regex.ToString()));
            Assert.That(deserializedRegex.Options, Is.EqualTo(regex.Options));
        }
    }
}
