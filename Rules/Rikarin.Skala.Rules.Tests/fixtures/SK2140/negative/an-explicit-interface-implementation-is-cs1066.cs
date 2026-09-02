// ⚠ The compiler already reports this one, and reports it better: CS1066 says the default can
// never be used at all, because an explicit implementation cannot be called through this type.
// ADR-008 hosts it, so the rule declines the whole shape.
namespace Fixtures {
    interface IStore {
        void Save(string key, bool overwrite = false);
    }

    sealed class FileStore : IStore {
        void IStore.Save(string key, bool overwrite = true) { }
    }
}
