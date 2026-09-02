// `ready && false` is `false`, but the rewrite drops `ready`'s evaluation. Not this concept.
class C {
    public static bool Run(bool ready) => ready && false;
}
