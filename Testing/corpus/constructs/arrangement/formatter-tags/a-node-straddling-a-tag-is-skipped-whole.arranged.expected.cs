// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaCleanup generated=2026-08-28
namespace FormatterTags;

public class ANodeStraddlingATagIsSkippedWhole {
    // The signature is outside the region and the body is inside it, so the node crosses the tag.
    // Skipping the whole node is the safe reading: `System.Int32` is not rewritten either, and nor
    // is the redundant `private`.
    int Straddles() =>
        // @formatter:off
        3;
    // @formatter:on

    // The region is *inside* this method rather than crossing it, so the method is not straddling —
    // and the body-style rewrite would still eat the tags if only the spans were consulted.
    public List<int> Contains() =>
        // @formatter:off
        new();

    // @formatter:on
    // Nothing here is protected.
    public List<int> Outside() => new();
}
