// Implicit implementations are reported and explicit ones are not: the compiler says nothing at
// all here, and CS1066 covers only the explicit form.
namespace Fixtures {
    interface IStore {
        void Save(string key, bool overwrite = false);
    }

    sealed class FileStore : IStore {
        public void Save(string key, bool overwrite = true) { }
    }
}
