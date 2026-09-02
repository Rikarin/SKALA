// ⚠ The fixture that actually reaches the method-type-parameter guard. `a-generic-method.cs` does
// not: its two receivers are `IEnumerable<T>` for two *different* `T`, so the receiver-type symbol
// comparison declines it first and removing the type-parameter check changes nothing there. Here the
// receiver is plain `string` and identical across both methods, so the type parameter is the only
// thing standing between this class and a finding — and `extension(string value)` has nowhere to put
// the `<T>`.
namespace Fixtures {
    static class TypeParameterHelpers {
        public static string Describe<T>(this string value, T item) => value + item;

        public static string Wrap<T>(this string value, T item) => value + item + value;
    }
}
