// The guard would be deleted with the method.
using System;

namespace Fixtures {
    sealed class Guarded {
        internal string Render(string text) {
            ArgumentNullException.ThrowIfNull(text);
            return Render(text, 4);
        }

        internal string Render(string text, int indent) => new string(' ', indent) + text;
    }
}
