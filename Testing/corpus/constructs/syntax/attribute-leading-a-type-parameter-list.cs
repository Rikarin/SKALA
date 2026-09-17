using System;

// An attribute on a type parameter — `void D<[Mark] T>()` — occurred nowhere in the corpus, in any
// position: `generic-attributes.cs` puts a generic attribute on a class, a property, a return, a
// parameter and a local function and never on a type parameter, and SK-DIV-0114 measured the shape
// for `attribute-section.cs` and left it out because of the space this file is about. The leading
// position is the one #373 found: `SpaceRules.BeforeOpenBracket` answered an attribute list with
// "whatever precedes decides", and a `<` on its own clings to nothing, so `D< [Mark] T>` came out
// with a space the oracle removes. The gap is the angle's, governed by
// `space_within_type_parameter_angles` — at `true` the oracle writes `D< [Mark] T >` — and this file
// pins the export's `false` on a method, a class, a delegate and a local function, written closed and
// spaced. The comma-led parameter and the attributed method parameters are the controls that were
// never wrong. `Mark` is declared here because `[Obsolete]`, which #373 was reported with, is not
// valid on a generic parameter and the fixture is meant to compile.
[AttributeUsage(AttributeTargets.GenericParameter | AttributeTargets.Parameter)]
sealed class MarkAttribute : Attribute { }

class AttributeLeadingATypeParameterList<[Mark] T> {
    void Method<[Mark] U>() { }

    void MethodSpaced< [Mark] U>() { }

    void Trailing<U, [Mark] V>() { }

    void Parameter([Mark] int a) { }

    void SecondParameter(int a, [Mark] int b) { }

    void Local() {
        void Inner<[Mark] U>() { }

        void InnerSpaced< [Mark] U>() { }

        Inner<int>();
        InnerSpaced<int>();
    }
}

class AttributeLeadingATypeParameterListSpaced< [Mark] T> { }

delegate void Handler<[Mark] T>(T value);

delegate void HandlerSpaced< [Mark] T>(T value);
