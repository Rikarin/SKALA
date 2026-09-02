// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
namespace Constructs.Breaks;

// The fill's first point is the gap after the `<`, so a single type parameter wider than the margin
// is what puts a break there. Its own file rather than a case in type-parameter-list.cs, because
// that file is the oracle fixture for align_multiline_type_parameter_list and this shape is the one
// Skala cannot follow at that key's other value: the alignment column is read after the anchor's gap
// has been written, and here that gap is the break, so the group the break belongs to is not open
// yet and the writer renders it flat. Pinning it beside the aligned cases would pin a divergence.
public class SingleTypeParameter {
    public void OneParameterWiderThanTheMargin<
        TAnAbsolutelyEnormousSingleTypeParameterNameThatOverflowsTheMarginOnItsOwn>() { }
}
