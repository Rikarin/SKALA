// A user-defined `>` is somebody else's semantics, and rewriting it to `>=` would be rewriting an
// operator this rule has not read.
class Position {
    public static bool operator >(Position left, int right) => true;

    public static bool operator <(Position left, int right) => false;
}

class C {
    bool Compare(Position position) => position > 0;
}
