// analyzer-option: dotnet_code_quality.SK7081.threshold = 1
// ⚠ A dependency hidden inside a generic argument is still a dependency, and this fixture is the
// one that proves it: `Alpha` and `Beta` are never written inside `Holder`. Only the walk over the
// constructed type's arguments finds them, so `Holder` measures three — `Box`, `Alpha`, `Beta` —
// rather than the one an implementation that read the written name alone would report.
using BoxedAlpha = Box<Alpha>;
using BoxedBeta = Box<Beta>;

class Alpha { }

class Beta { }

class Box<T> {
    public T? Item { get; set; }
}

class Holder {
    BoxedAlpha? alpha;

    BoxedBeta? beta;
}
