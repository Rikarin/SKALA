// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
using System.Numerics;

class Fill {
    // ⚠ An idempotency shape rather than a fidelity one, and it took a 4 708-file tree to find. One
    // element of the filled line is exactly wide enough that the fill keeps it and its own argument
    // list then does not fit, leaving `new Vector3(` at the end of a filled line — which the second
    // pass sees as a multi-line item and breaks before. It is stable only while the width a fill
    // point measures and the width the item's own group measures are the same number.
    void M() {
        var directions = new[] {
            new Vector3(0f, 10f, 0f), new Vector3(0f, -10f, 0f), new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 10f),
            new Vector3(3f, 4f, 5f)
        };
        Use(directions);
    }

    static void Use(Vector3[] v) { }
}
