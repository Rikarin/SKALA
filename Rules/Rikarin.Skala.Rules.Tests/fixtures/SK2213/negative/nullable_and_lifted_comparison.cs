// A lifted comparison is neither true nor false in the way the rewrite assumes, and there is no
// `IndexOf` under it here either.
class C {
    bool M(int? value) => value > 0;
}
