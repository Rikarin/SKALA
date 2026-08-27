using System.Runtime.CompilerServices;

// ⚠ One seam, and it exists for one property. CloneDetector's promise is that a hash collision can
// never produce a finding (docs/plan/09 § "Duplication", step 3), and the only way to test that
// without waiting for a 2⁻⁶⁴ event is to force every window into one bucket from inside the
// assembly. The alternative — a public knob for collapsing hashes — would put a testing switch on
// the API that the check pipeline calls.
[assembly: InternalsVisibleTo("Rikarin.Skala.Analysis.Tests")]
