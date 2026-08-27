using System.Threading.Tasks;

public sealed class Outcome<T> {
    public Outcome(T result) => Result = result;

    public T Result { get; }
}

public sealed class Reader {
    public async Task<int> ReadAsync(Outcome<int> outcome) {
        await Task.Yield();
        return outcome.Result;
    }
}
