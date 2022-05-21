using System;
using System.Collections.Generic;

namespace OpenUGD.Utils
{
    public abstract class Persist<T> : Persist where T : Persist, new()
    {
        public new T this[string key] {
            get {
                var result = (T)GetPersistance(key);
                if (result == null)
                {
                    result = new T();
                    Initialize(result, key, Provider, this);
                    SetPersistance(key, result);
                }

                return result;
            }
        }
    }

    public class Persist
    {
        public static readonly Type[] AvailableTypes =
            { typeof(int), typeof(string), typeof(float), typeof(long), typeof(bool) };

        private readonly Dictionary<string, Persist> _paths = new();
        private float? _floatCache;
        private bool _initialized;
        private int? _intCache;
        private long? _longCache;
        private string _stringCache;
        private bool _stringHasCache;

        public Persist()
        {
        }

        public Persist(IPersistProvider persistProvider) => Initialize(this, null, persistProvider, null);

        public IPersistProvider Provider { get; private set; }

        public Persist this[string key] {
            get {
                Persist result;
                if (!_paths.TryGetValue(key, out result))
                {
                    _paths[key] = result = Initialize(new Persist(), key, Provider, this);
                }

                return result;
            }
        }

        public string FullPath { get; private set; }

        public Persist Parent { get; private set; }

        public string Key { get; private set; }

        public long LongValue {
            get {
                if (_longCache.HasValue)
                {
                    return _longCache.Value;
                }

                var left = (uint)Provider.GetInt(GetLeftLongPath(FullPath));
                var right = (uint)Provider.GetInt(GetRightLongPath(FullPath));
                return CombineToLong(left, right);
            }
            set {
                _longCache = value;
                var left = GetLeft(value);
                var right = GetRight(value);
                var leftInt = (int)left;
                var rightInt = (int)right;
                Provider.SetInt(GetLeftLongPath(FullPath), leftInt);
                Provider.SetInt(GetRightLongPath(FullPath), rightInt);
                Provider.Save();
            }
        }

        public bool BoolValue {
            get {
                if (_intCache.HasValue)
                {
                    return _intCache.Value != 0;
                }

                return Provider.GetInt(FullPath) != 0;
            }
            set {
                _intCache = value ? 1 : 0;
                Provider.SetInt(FullPath, value ? 1 : 0);
                Provider.Save();
            }
        }

        public int IntValue {
            get {
                if (_intCache.HasValue)
                {
                    return _intCache.Value;
                }

                return Provider.GetInt(FullPath);
            }
            set {
                _intCache = value;
                Provider.SetInt(FullPath, value);
                Provider.Save();
            }
        }

        public float FloatValue {
            get {
                if (_floatCache.HasValue)
                {
                    return _floatCache.Value;
                }

                return Provider.GetFloat(FullPath);
            }
            set {
                _floatCache = value;
                Provider.SetFloat(FullPath, value);
                Provider.Save();
            }
        }

        public string StringValue {
            get {
                if (_stringHasCache)
                {
                    return _stringCache;
                }

                return Provider.GetString(FullPath);
            }
            set {
                _stringCache = value;
                _stringHasCache = true;
                Provider.SetString(FullPath, value);
                Provider.Save();
            }
        }

        public static string ConcatPath(string fullPath, string key) => fullPath + '/' + key;

        protected static Persist Initialize(Persist persistence, string key, IPersistProvider persistProvider,
            Persist parent)
        {
            if (persistence._initialized)
            {
                throw new InvalidOperationException("Persist have initialized");
            }

            persistence.Key = key;
            persistence.Provider = persistProvider;
            persistence.Parent = parent;
            if (parent != null)
            {
                persistence.FullPath = ConcatPath(parent.FullPath, key);
            }
            else
            {
                persistence.FullPath = key;
            }

            persistence._initialized = true;
            return persistence;
        }

        protected Persist GetPersistance(string key)
        {
            Persist result;
            _paths.TryGetValue(key, out result);
            return result;
        }

        protected void SetPersistance(string key, Persist value) => _paths[key] = value;

        public void SetDefaultValue(object value)
        {
            if (value is int)
            {
                if (!Provider.HasKey(FullPath))
                {
                    _intCache = (int)value;
                }
                else
                {
                    _intCache = Provider.GetInt(FullPath);
                }
            }
            else if (value is bool)
            {
                if (!Provider.HasKey(FullPath))
                {
                    _intCache = (bool)value ? 1 : 0;
                }
                else
                {
                    _intCache = Provider.GetInt(FullPath);
                }
            }
            else if (value is float)
            {
                if (!Provider.HasKey(FullPath))
                {
                    _floatCache = (float)value;
                }
                else
                {
                    _floatCache = Provider.GetFloat(FullPath);
                }
            }
            else if (value is string)
            {
                if (!Provider.HasKey(FullPath))
                {
                    _stringCache = (string)value;
                    _stringHasCache = true;
                }
                else
                {
                    _stringHasCache = true;
                    _stringCache = Provider.GetString(FullPath);
                }
            }
            else if (value is long)
            {
                if (!Provider.HasKey(GetRightLongPath(FullPath)))
                {
                    _longCache = (long)value;
                }
                else
                {
                    var right = Provider.GetInt(GetRightLongPath(FullPath));
                    var left = Provider.GetInt(GetLeftLongPath(FullPath));
                    _longCache = CombineToLong((uint)left, (uint)right);
                }
            }
            else
            {
                throw new ArgumentException(
                    "type not supported: " + (value != null ? value.GetType().FullName : "null"));
            }
        }

        public T GetValue<T>()
        {
            var type = typeof(T);
            return (T)GetValue(type);
        }

        public object GetValue(Type type)
        {
            if (type == typeof(int))
            {
                return IntValue;
            }

            if (type == typeof(bool))
            {
                return BoolValue;
            }

            if (type == typeof(float))
            {
                return FloatValue;
            }

            if (type == typeof(string))
            {
                return StringValue;
            }

            if (type == typeof(long))
            {
                return LongValue;
            }

            throw new ArgumentException("type not supported: " + type.FullName);
        }

        public void SetValue(object value)
        {
            if (value == null)
            {
                IntValue = 0;
                BoolValue = false;
                FloatValue = 0;
                StringValue = null;
                LongValue = 0;
            }
            else
            {
                var type = value.GetType();
                if (type == typeof(int))
                {
                    IntValue = (int)value;
                }
                else if (type == typeof(bool))
                {
                    BoolValue = (bool)value;
                }
                else if (type == typeof(float))
                {
                    FloatValue = (float)value;
                }
                else if (type == typeof(string))
                {
                    StringValue = (string)value;
                }
                else if (type == typeof(long))
                {
                    LongValue = (long)value;
                }
                else
                {
                    throw new ArgumentException("type not supported: " + type.FullName);
                }
            }
        }

        public void ClearCache()
        {
            _floatCache = null;
            _intCache = null;
            _longCache = null;
            _stringCache = null;
            _stringHasCache = false;
        }

        private static string GetLeftLongPath(string path) => path + "@32";

        private static string GetRightLongPath(string path) => path + "@0";

        private static uint GetLeft(long value)
        {
            var newValue = (ulong)value >> 32;
            return (uint)newValue;
        }

        private static uint GetRight(long value)
        {
            var newValue = 0xFFFFFFFF & (ulong)value;
            return (uint)newValue;
        }

        private static long CombineToLong(uint left, uint right) => (long)(((ulong)left << 32) | right);
    }
}
