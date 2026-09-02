using System.Collections.Generic;

// `List<T>` is invariant, so the parameter is in an invariant position even though it reads as an
// ordinary return type. `out T` here is CS1961.
public interface IBuilder<T> {
    List<T> AsList();
}
