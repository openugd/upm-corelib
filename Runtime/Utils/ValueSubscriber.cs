using System;

namespace OpenUGD.Utils
{
    public class ValueSubscriber<T> where T : IEquatable<T>
    {
        private readonly Signal<ValueSubscriber<T>> _onChange;
        private T _current;

        public ValueSubscriber(Lifetime lifetime, T defaultValue = default)
        {
            _onChange = new Signal<ValueSubscriber<T>>(lifetime);
            _current = Prev = defaultValue;
        }

        public virtual T Current {
            get => GetValue();
            set => SetValue(value);
        }

        public T Prev { get; private set; }

        public static implicit operator T(ValueSubscriber<T> value) => value.Current;

        public void ForceFire() => _onChange.Fire(this);

        public virtual void SubscribeOnChange(Lifetime lifetime, Action<ValueSubscriber<T>> listener) =>
            _onChange.Subscribe(lifetime, listener);

        protected virtual void SetValue(T value)
        {
            var last = _current;
            if (!ReferenceEquals(last, value) && (ReferenceEquals(last, null) || !last.Equals(value)))
            {
                Prev = last;
                _current = value;
                _onChange.Fire(this);
            }
        }

        protected virtual T GetValue() => _current;
    }
}
