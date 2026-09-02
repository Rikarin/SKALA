using System.Collections.Generic;

interface IStore {
    void Add(KeyValuePair<string, int> entry);
}

interface IKeyedStore : IStore {
    void Add(string key, int value);
}

static class Call {
    // The `IDictionary` shape exactly: the hidden overload takes one argument and the call passes
    // two, so it is not applicable and there is nothing to report.
    public static void Run(IKeyedStore store) => store.Add("k", 1);
}
