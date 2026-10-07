// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
namespace Constructs.Breaks;

// SK-DIV-0199 (issue #434). A block comment after a declaration's last attribute section leaves the
// gap to the author: the placement key's break does not survive the comment, so a declaration written
// after the comment stays there and one written below it stays below, and a declaration too long for
// the line chops its own parameters with the attribute still on it. Between two sections the point
// survives as it does everywhere else (SK-DIV-0165), and breaks after the comment.
[Obsolete] /* c */ public class TopLevelAttributeComment { }

[Obsolete] /* c */
[Serializable]
public class TopLevelTwoSections { }

public class CommentAfterTheLastAttribute {
    [Obsolete] /* c */ public void Method() { }
    [Obsolete] /* c */ public int Field;
    public int Property { [Obsolete] /* c */ get; set; }

    [Obsolete] /* c */
    [Serializable]
    public void TwoSections() { }

    [Obsolete]
    [Serializable] /* c */ public void CommentAfterTheSecond() { }

    [Obsolete] /* c
       d */ public void MultiLineComment() { }

    [Obsolete] /** c */ public void StarredComment() { }

    [Obsolete] /* c */
    public void BrokenAfterTheComment() { }

    [Obsolete]
    /* c */ public void CommentOnItsOwnLine() { }

    [Obsolete] /* c */ public event Action Event;
    [Obsolete] /* c */ public CommentAfterTheLastAttribute() { }

    [Obsolete] /* c */ public void TooLongForTheLine(
        int alphaParameterValue,
        int betaParameterValue,
        int gammaValueXYZW
    ) { }

    void Locals() {
        [Obsolete] /* c */ void Local() { }
        Local();
    }
}

public record CommentAfterARecordFieldAttribute([Obsolete] /* c */ int A, [property: Obsolete] /* c */ int B);
