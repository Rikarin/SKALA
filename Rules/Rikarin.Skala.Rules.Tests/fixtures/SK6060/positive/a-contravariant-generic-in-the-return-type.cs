using System;

// ⚠ `Action<in T>` flips the position, so a parameter sitting inside a *return* type here is
// contravariant and the modifier the rule offers is `in`, not `out`. This file started life as a
// negative on the assumption that a flipped position was no position at all; the compiler accepts
// `in T` and refuted it.
public interface IHandlerSource<T> {
    Action<T> GetHandler();
}
