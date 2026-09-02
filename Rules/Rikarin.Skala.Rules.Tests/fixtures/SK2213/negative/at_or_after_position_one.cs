// ⚠ The escape hatch, and the reason the rule can be a warning at all. `> 0` is a correct test
// when "found, but not at the start" is what was meant, and the rule cannot tell the two readings
// apart — so `>= 1`, which says the same thing with no second reading, is deliberately not reported.
// A codebase that means it says so once and never sees the rule again.
class C {
    bool NotAtTheStart(string path) => path.IndexOf(':') >= 1;
}
