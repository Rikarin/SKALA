// analyzer-option: dotnet_code_quality.SK7080.threshold = 1
// Nothing here has a base class at all, so the shallowest possible threshold still finds nothing.
// A struct and a record struct have no base chain a person can write; `object` is not counted.
namespace Fixtures;

class Plain { }

struct Value {
    public int Count;
}

record struct Point(int X, int Y);

record Named(string Text);

static class Helpers {
    public static int Twice(int value) => value * 2;
}
