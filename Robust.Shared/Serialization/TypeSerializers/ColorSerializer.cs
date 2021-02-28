using JetBrains.Annotations;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Manager.Result;
using Robust.Shared.Serialization.Markdown;

namespace Robust.Shared.Serialization.TypeSerializers
{
    [TypeSerializer]
    public class ColorSerializer : ITypeSerializer<Color, ValueDataNode>
    {
        public DeserializationResult<Color> Read(ValueDataNode node, ISerializationContext? context = null)
        {
            var deserializedColor = Color.TryFromName(node.Value, out var color)
                ? color :
                Color.FromHex(node.Value);

            return new DeserializedValue<Color>(deserializedColor);
        }

        public DataNode Write(Color value, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return new ValueDataNode(value.ToHex());
        }

        [MustUseReturnValue]
        public Color Copy(Color source, Color target)
        {
            return new(source.R, source.G, source.B, source.A);
        }
    }
}
