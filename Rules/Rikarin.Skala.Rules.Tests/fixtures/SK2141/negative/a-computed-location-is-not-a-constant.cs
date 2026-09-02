// A relay that already holds a real file path passes it on. Only a *constant* argument to a
// location parameter is a fabrication; a value the program computed is the caller's own, and
// this is how a logging façade forwards one.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Facade {
        public void Trace(string message, [CallerFilePath] string? file = null) {
            Inner(message, file ?? string.Empty);
        }

        static void Inner(string message, [CallerFilePath] string? file = null) { }
    }
}
