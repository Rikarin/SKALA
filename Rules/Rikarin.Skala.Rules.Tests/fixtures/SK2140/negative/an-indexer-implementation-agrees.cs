// Indexers carry parameter lists and are analysed the same way, so the agreeing case has to be
// pinned for them too — otherwise the rule could fire on every indexer and no fixture would say.
namespace Fixtures {
    interface ILookup {
        string this[string key, bool exact = false] { get; }
    }

    sealed class Table : ILookup {
        public string this[string key, bool exact = false] => key;
    }
}
