using System;

public sealed class CacheMissException<TKey> : Exception {
    public CacheMissException(TKey key) : base(key?.ToString()) {
        Key = key;
    }

    public TKey Key { get; }
}
