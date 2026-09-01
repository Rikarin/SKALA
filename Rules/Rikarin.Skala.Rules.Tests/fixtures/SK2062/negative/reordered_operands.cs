// `a && b` and `b && a` are different trees, and the rule compares trees. They also evaluate in a
// different order, so calling them the same would be wrong as well as unhelpful.
class C {
    void M(bool a, bool b) {
        if (a && b) {
            First();
        } else if (b && a) {
            Second();
        }
    }

    static void First() { }

    static void Second() { }
}
