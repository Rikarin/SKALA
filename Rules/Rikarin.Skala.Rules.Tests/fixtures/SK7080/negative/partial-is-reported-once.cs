// analyzer-option: dotnet_code_quality.SK7080.threshold = 20
// A partial class names its base on exactly one part, which is the part this rule looks at. With
// the threshold far above anything here, no part reports — and the fixture documents that the
// other parts are skipped before the semantic model is asked anything.
namespace Fixtures;

class Root { }

partial class Split : Root { }

partial class Split {
    public int First { get; set; }
}

partial class Split {
    public int Second { get; set; }
}
