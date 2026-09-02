// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Alpha.Things;

// ⚠ Exists so that sort-and-remove.cs has a using that sorts BEFORE `System` ordinally. Without one,
// `dotnet_sort_system_directives_first` cannot be observed at all: every using in the file starts
// with "System", so hoisting System to the front is the order it was already in, and the option
// looks unimplemented when it is only unobservable.
public static class Tool {
    public static int Twice(int value) => value * 2;
}
