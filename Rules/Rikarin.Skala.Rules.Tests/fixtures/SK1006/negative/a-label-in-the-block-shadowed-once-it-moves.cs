// ⚠ #425: the block's own label moves out into the enclosing block, where a sibling block's label of
// the same name now shadows it (CS0158).
public static class Holder {
    public static int Run(System.IDisposable resource, int n) {
        {
            if (n > 1) {
                goto done;
            }

            n++;
        done:
            n++;
        }

        using (var held = resource) {
            if (n > 5) {
                goto done;
            }

            n *= 2;
        done:
            return n;
        }
    }
}
