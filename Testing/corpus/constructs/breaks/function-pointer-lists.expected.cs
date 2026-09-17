// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-17
namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). A function pointer's parameter list `<int, void>` and its calling
// convention list `unmanaged[Cdecl, …]` are both filled exactly as a tuple's components are: a
// kept break after a comma, before a comma or after the opening delimiter comes back as written
// with the next item one level in, and a list past the margin fills at its commas. Before this
// file neither had a plan — and neither had ever opened a delimited scope, the two being listed
// under each other's delimiters, so a break the fill added landed at the declaration's own column.
public unsafe class FunctionPointerLists {
    delegate*<int,
        void> AfterComma;

    delegate*<
        int, void> AfterOpen;

    delegate*<int
        , void> BeforeComma;

    delegate*<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin,
        SomeVeryLongTypeNameNumberTwoForTheMargin, void> TooLong;

    delegate* unmanaged[Cdecl,
        SuppressGCTransition]<int, void> ConventionAfterComma;

    delegate* unmanaged[
        Cdecl, SuppressGCTransition]<int, void> ConventionAfterOpen;

    delegate* unmanaged[Cdecl
        , SuppressGCTransition]<int, void> ConventionBeforeComma;

    delegate* unmanaged[Cdecl, SuppressGCTransition, MemberFunction, Stdcall, Thiscall, Fastcall, Cdecl,
        SuppressGCTransition]<int, void> ConventionTooLong;

    void Local() {
        delegate*<int,
            void> p = null;
        delegate*<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin,
            SomeVeryLongTypeNameNumberTwoForTheMargin, void> q = null;
    }
}

public class SomeVeryLongTypeNameNumberOneForTheMargin { }

public class SomeVeryLongTypeNameNumberTwoForTheMargin { }
