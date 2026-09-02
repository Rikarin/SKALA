// An attribute on the forwarding overload is intent: the declaration is doing a job the body does
// not show.
using System;

namespace Fixtures {
    sealed class Deprecated {
        [Obsolete("Pass the indent explicitly.")]
        internal string Render(string text) => Render(text, 4);

        internal string Render(string text, int indent) => new string(' ', indent) + text;
    }
}
