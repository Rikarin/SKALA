using System;
using System.IO;
using System.Threading.Tasks;

public sealed class Factory {
    // `async` goes in a different place in each of a lambda's spellings, and the delegate's return
    // type comes from a conversion the rule would have to re-check after the edit.
    public Func<Task<string>> Make(string path) {
        return () => {
            using (var reader = new StreamReader(path)) {
                return reader.ReadToEndAsync();
            }
        };
    }
}
