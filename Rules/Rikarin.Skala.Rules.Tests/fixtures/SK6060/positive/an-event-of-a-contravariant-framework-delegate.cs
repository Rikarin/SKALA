using System;

// ⚠ `EventHandler<TEventArgs>` is declared `in` on modern .NET. An event type is an input position
// and the delegate's own contravariance flips it back, so `out T` is legal — which is the opposite
// of what this file was written to assert.
public interface INotifier<T> {
    event EventHandler<T> Changed;
}
