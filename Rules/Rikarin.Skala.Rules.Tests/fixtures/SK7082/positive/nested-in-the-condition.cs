// A conditional used as another's condition. The parentheses are mandatory here and they are the
// only thing telling the reader where the boundary is.
namespace Fixtures;

class Gate {
    public static int Pick(bool a, bool b, bool c) => (a ? b : c) ? 1 : 0;
}
