using System;
using System.Threading.Tasks;

public sealed class Loader {
    // The `return null;` belongs to the delegate, whose return type is `string?` and not `Task`.
    public Task<Func<string?>> Read() {
        return Task.FromResult<Func<string?>>(() => {
                return null;
            }
        );
    }
}
