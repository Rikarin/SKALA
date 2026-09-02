// The span searches are covered, so the correct spelling on one of them is worth a fixture.
using System;

class C {
    bool Present(ReadOnlySpan<char> text) => text.IndexOf(':') >= 0;
}
