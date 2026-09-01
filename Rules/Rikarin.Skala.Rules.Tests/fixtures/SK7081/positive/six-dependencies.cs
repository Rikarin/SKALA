// analyzer-option: dotnet_code_quality.SK7081.threshold = 5
// Six distinct other types, over a threshold of five. They arrive by six different routes — a base
// class, a field, a property, a parameter, a local and a construction — because the measurement is
// the union of everything the declaration names, not a count of one syntactic position.
namespace Fixtures;

class Base { }

class Stored { }

class Exposed { }

class Passed { }

class Local { }

class Built { }

class Entangled : Base {
    Stored stored = new();

    public Exposed Value { get; set; } = new();

    public void Accept(Passed passed) {
        Local local = new();
        var built = new Built();
    }
}
