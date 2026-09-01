// analyzer-option: dotnet_code_quality.SK7081.threshold = 3
// ⚠ The wrapper is not charged for what its nested type depends on. `Coupled` names four other
// types and is measured on its own action; `Wrapper` names only `Coupled`, which is nested inside
// it and therefore not a dependency at all. Descending would report the same union once per level,
// and the number a reader could act on would be buried under one they could not.
namespace Fixtures;

class Alpha { }

class Beta { }

class Gamma { }

class Delta { }

class Wrapper {
    Coupled? held;

    sealed class Coupled {
        Alpha? alpha;

        Beta? beta;

        Gamma? gamma;
    }
}
