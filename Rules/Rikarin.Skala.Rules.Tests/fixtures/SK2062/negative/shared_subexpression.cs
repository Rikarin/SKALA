// Two conditions over the same subject that share a term are two questions, not one repeated.
enum Kind { A, B, C }

class Node {
    public Kind Kind;
}

class C {
    void M(Node x) {
        if (x.Kind == Kind.A) {
            First();
        } else if (x.Kind == Kind.B) {
            Second();
        } else if (x.Kind == Kind.C) {
            Third();
        }
    }

    static void First() { }

    static void Second() { }

    static void Third() { }
}
