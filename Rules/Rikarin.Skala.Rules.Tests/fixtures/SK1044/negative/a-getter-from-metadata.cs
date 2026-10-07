// ⚠ #423: a property whose body cannot be seen is not storage either. `Uri.Fragment` is computed on
// every read.
public sealed class Links {
    public static bool HasNoFragment(System.Uri uri) => uri.Fragment == null || uri.Fragment.Length == 0;
}
