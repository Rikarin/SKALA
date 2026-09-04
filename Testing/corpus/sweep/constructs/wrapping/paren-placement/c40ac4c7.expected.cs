// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
public record ParenPlacementRecord(
    int FirstParameterNameHere,
    int SecondParameterNameHere,
    int ThirdParameterNameHere,
    int FourthParameterName);

public class ParenPlacement {
    // resharper_csharp_wrap_before_declaration_lpar, resharper_csharp_wrap_before_invocation_lpar
    // and resharper_csharp_wrap_before_primary_constructor_declaration_lpar: whether the opening
    // parenthesis of a chopped list gets a line of its own. All three are false in the export, so
    // this fixture is the unbroken-parenthesis shape and the option unit is what flips them.
    void Declaration(
        int firstParameterNameHere,
        int secondParameterNameHere,
        int thirdParameterNameHere,
        int fourthParameterNameXy
    ) { }

    void Invocation() {
        SomeMethodWithARatherLongName
        (
            firstArgumentValueNameHere,
            secondArgumentValueNameHere,
            thirdArgumentValueNameHereXy
        );
    }
}
