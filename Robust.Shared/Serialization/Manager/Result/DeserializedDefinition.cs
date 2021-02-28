﻿using System;
using Robust.Shared.IoC;

namespace Robust.Shared.Serialization.Manager.Result
{
    public class DeserializedDefinition<T> : DeserializationResult<T>, IDeserializedDefinition where T : new()
    {
        public DeserializedDefinition(T value, DeserializedFieldEntry[] mapping)
        {
            Value = value;
            Mapping = mapping;
        }

        public override T Value { get; }

        public DeserializedFieldEntry[] Mapping { get; }

        public override object? RawValue => Value;

        public override DeserializationResult PushInheritanceFrom(DeserializationResult source)
        {
            var dataDef = source.Cast<DeserializedDefinition<T>>();
            if (dataDef.Mapping.Length != Mapping.Length)
                throw new ArgumentException($"Mappinglength mismatch in PushInheritanceFrom ({typeof(T)})");

            var newMapping = new DeserializedFieldEntry[Mapping.Length];

            for (var i = 0; i < dataDef.Mapping.Length; i++)
            {
                if(Mapping[i].Mapped)
                {
                    newMapping[i] = Mapping[i].Copy();
                }
                else
                {
                    newMapping[i] = dataDef.Mapping[i].Copy();
                }
            }

            return IoCManager.Resolve<ISerializationManager>().CreateDataDefinition<T>(newMapping);
        }

        public override DeserializationResult Copy()
        {
            var newMapping = new DeserializedFieldEntry[Mapping.Length];

            for (var i = 0; i < Mapping.Length; i++)
            {
                newMapping[i] = Mapping[i].Copy();
            }

            return IoCManager.Resolve<ISerializationManager>().CreateDataDefinition<T>(newMapping);
        }
    }
}