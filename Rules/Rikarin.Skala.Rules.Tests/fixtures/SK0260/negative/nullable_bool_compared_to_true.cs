// `maybe == true` is `false` when `maybe` is null; `maybe` alone is not even a `bool`.
class C {
    public static int Run(bool? maybe) {
        if (maybe == true) {
            return 1;
        }

        return 0;
    }
}
