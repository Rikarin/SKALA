// An indexer is a call, so the right operand may do work the `&` was written to force.
class C {
    bool M(bool ready, bool[] flags) => ready & flags[0];
}
