// No caller-info attribute anywhere. An argument restating an ordinary default is SK0232's
// finding, and this rule must be silent on it or the two would report one span twice.
namespace Fixtures {
    sealed class Ordinary {
        public void Run() {
            Trace("started", false);
        }

        static void Trace(string message, bool verbose = false) { }
    }
}
