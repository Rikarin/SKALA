// analyzer-option: dotnet_code_quality.SK7081.threshold = 1
// ⚠ A type that organises itself with nested helpers is not coupled to somebody else's design: the
// file a reader opens contains both, and they move together by construction. The type itself, the
// types nesting it and the types it nests are all excluded — which is what makes this fixture
// silent at the shallowest threshold despite eight names being written.
namespace Fixtures;

class Outer {
    public sealed class Inner {
        public Outer? Owner;

        public Sibling? Next;
    }

    public sealed class Sibling {
        public Inner? Previous;

        public Outer? Owner;
    }

    Inner? first;

    Sibling? second;

    public Outer Self() => this;
}
