// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
namespace Constructs.Breaks;

// wrap_before_type_parameter_langle and align_multiline_type_parameter_list. A type parameter list
// wider than the margin wraps as a fill: the break lands at the last comma that fits, and the `>`
// stays with whatever parameter ends the list.
public class TypeParameterLists {
    public void WiderThanTheMargin<TFirstParameterName, TSecondParameterName, TThirdParameterName, TFourthParameterName,
        TFifthName, TSixth>() { }

    // Fits: nothing moves at any value of either key.
    public void Short<TFirst, TSecond>(TFirst a) { }
}

public class WideDeclaration<TFirstParameterName, TSecondParameterName, TThirdParameterName, TFourthParameterName,
    TFifthName, TSixthName> {
    public TFirstParameterName? Value { get; set; }
}
