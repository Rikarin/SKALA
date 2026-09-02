// fixture-option: LangVersion = 9
// ⚠ The rule's floor is C# 10, and a file-scoped namespace is a syntax error below it — so telling
// this file to use one would be telling it not to compile. Before `// fixture-option:` existed
// every fixture compiled at `Preview` and this guard had no fixture at all: the shape it declines
// is *the same shape* the positive fixtures carry, and only the language version tells them apart
// (#317).
using System;

namespace Sample {
    class Holder {
        public int Value { get; set; }
    }
}
