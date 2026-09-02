using System.Collections.Generic;

public interface ISource<T> {
    IEnumerable<T> All();

    IReadOnlyList<T> Recent(int count);
}
