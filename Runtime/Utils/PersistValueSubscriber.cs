using System;

namespace OpenUGD.Utils
{
    public class PersistValueSubscriber<T> : ValueSubscriber<T> where T : IEquatable<T>
    {
        private readonly IConverter _converter;

        public PersistValueSubscriber(Lifetime lifetime, Persist persistence, T defaultValue) : this(lifetime,
            persistence, defaultValue, new Converter<T>(a => a, a => a))
        {
        }

        public PersistValueSubscriber(Lifetime lifetime, Persist persistence, T defaultValue, IConverter converter) :
            base(lifetime, defaultValue)
        {
            _converter = converter;
            Initialize(lifetime, persistence, defaultValue);
        }

        private void Initialize(Lifetime lifetime, Persist persistence, T defaultValue)
        {
            persistence.SetDefaultValue(_converter.Serialize(defaultValue));
            Current = _converter.Deserialize(persistence.GetValue(_converter.PersistType));
            SubscribeOnChange(lifetime, value => {
                var serialized = _converter.Serialize(value.Current);
                persistence.SetValue(serialized);
            });
        }

        public interface IConverter
        {
            Type PersistType { get; }
            T Deserialize(object value);
            object Serialize(T value);
        }


        public class Converter<TPersist> : IConverter
        {
            private readonly Func<TPersist, T> _deserializer;
            private readonly Func<T, TPersist> _serializer;

            public Converter(Func<T, TPersist> serializer, Func<TPersist, T> deserializer)
            {
                _serializer = serializer;
                _deserializer = deserializer;
            }

            public Type ValueType => typeof(T);

            public Type PersistType => typeof(TPersist);

            public T Deserialize(object value) => _deserializer((TPersist)value);

            public object Serialize(T value) => _serializer(value);
        }
    }
}
