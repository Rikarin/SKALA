using System.Collections.Generic;

public interface IView<T> : IReadOnlyList<T> {
    T First { get; }
}
