// ⚠ The third nullable context, and the only one in which the rule's annotation guard is doing
// work of its own. `#nullable disable annotations` leaves the flow analysis running — so the
// initialiser's state really is NotNull — while making the `?` meaningless, which is CS8632. A rule
// that read only the flow state would fire here and offer to remove a `?` the compiler is already
// complaining about, on a declaration where removing it changes nothing.
//
// Without this fixture, deleting the `AnnotationsEnabledAt` check turns nothing red: in a fully
// disabled context the flow state is `None` rather than `NotNull`, so the two guards mask each
// other and the sabotage passes. That masking is what this file exists to break.
#nullable enable
namespace Fixtures {
    sealed class HalfDisabled {
#nullable disable annotations
        public int Measure() {
            string? name = "anonymous";
            return name.Length;
        }
#nullable restore annotations
    }
}
