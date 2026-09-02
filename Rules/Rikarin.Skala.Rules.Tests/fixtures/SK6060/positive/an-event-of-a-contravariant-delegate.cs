using System;

public interface IWatcher<T> {
    event Action<T> Changed;

    T Current { get; }
}
