﻿using System.Collections.Generic;

namespace Robust.Shared.Serialization.Manager.Result
{
    public class DeserializedReadOnlyDictionary<TDict, TKey, TValue> :
        DeserializationResult<TDict>
        where TKey : notnull
        where TDict : IReadOnlyDictionary<TKey, TValue>
    {
        public delegate TDict Create(Dictionary<TKey, TValue> elements);

        public DeserializedReadOnlyDictionary(
            TDict? value,
            IReadOnlyDictionary<DeserializationResult, DeserializationResult> mappings,
            Create createDelegate)
        {
            Value = value;
            Mappings = mappings;
            CreateDelegate = createDelegate;
        }

        public override TDict? Value { get; }

        public IReadOnlyDictionary<DeserializationResult, DeserializationResult> Mappings { get; }

        public Create CreateDelegate { get; }

        public override object? RawValue => Value;

        public override DeserializationResult PushInheritanceFrom(DeserializationResult source)
        {
            var sourceRes = source.Cast<DeserializedReadOnlyDictionary<TDict, TKey, TValue>>();
            var valueDict = new Dictionary<TKey, TValue>();
            var mappingDict = new Dictionary<DeserializationResult, DeserializationResult>();

            foreach (var (keyRes, valRes) in sourceRes.Mappings)
            {
                var newKeyRes = keyRes.Copy().Cast<DeserializationResult<TKey>>();
                var newValueRes = valRes.Copy().Cast<DeserializationResult<TValue>>();

                valueDict.Add(newKeyRes.Value, newValueRes.Value);
                mappingDict.Add(newKeyRes, newValueRes);
            }

            foreach (var (keyRes, valRes) in Mappings)
            {
                var newKeyRes = keyRes.Copy().Cast<DeserializationResult<TKey>>();
                var newValueRes = valRes.Copy().Cast<DeserializationResult<TValue>>();

                valueDict.Add(newKeyRes.Value, newValueRes.Value);
                mappingDict.Add(newKeyRes, newValueRes);
            }

            return new DeserializedReadOnlyDictionary<TDict, TKey, TValue>(CreateDelegate(valueDict), mappingDict, CreateDelegate);
        }

        public override DeserializationResult Copy()
        {
            var valueDict = new Dictionary<TKey, TValue>();
            var mappingDict = new Dictionary<DeserializationResult, DeserializationResult>();

            foreach (var (keyRes, valRes) in Mappings)
            {
                var newKeyRes = keyRes.Copy().Cast<DeserializationResult<TKey>>();
                var newValueRes = valRes.Copy().Cast<DeserializationResult<TValue>>();

                valueDict.Add(newKeyRes.Value, newValueRes.Value);
                mappingDict.Add(newKeyRes, newValueRes);
            }

            return new DeserializedReadOnlyDictionary<TDict, TKey, TValue>(CreateDelegate(valueDict), mappingDict, CreateDelegate);
        }
    }
}