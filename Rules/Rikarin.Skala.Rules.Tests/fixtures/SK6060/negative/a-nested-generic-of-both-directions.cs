using System;

// The parameter reaches a covariant position through the `Func` result and a contravariant one
// through its argument, so neither modifier is available.
public interface IMapperSource<T> {
    Func<T, T> GetMapper();
}
