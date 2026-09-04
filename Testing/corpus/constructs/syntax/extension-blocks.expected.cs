// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
using System;
using System.Collections.Generic;
using System.Linq;

// ExtensionBlockDeclaration — C# 14's `extension(receiver) { … }` — occurred once in the whole corpus,
// and that once was inside a pathological file whose point is something else. It is a new declaration
// form: a braced block that owns a parameter list and holds members, which means the brace-placement,
// blank-line and member-arrangement keys all apply to a shape none of them has an example of.
static class ExtensionBlocks {
    extension(string subject) {
        public bool IsBlank => string.IsNullOrWhiteSpace(subject);

        public string Repeated(int times) => string.Concat(Enumerable.Repeat(subject, times));

        public char First {
            get => subject[0];
        }
    }

    extension<T>(IReadOnlyList<T> subjects) where T : IComparable<T> {
        public T Largest => subjects.Aggregate(static (left, right) => left.CompareTo(right) >= 0 ? left : right);

        public IReadOnlyList<T> LargerThan(T floor) => [.. subjects.Where(subject => subject.CompareTo(floor) > 0)];
    }

    // A receiver wide enough that the extension's own parameter list has to be wrapped, and a member
    // wide enough that its body has to be.
    extension(IReadOnlyDictionary<string, IReadOnlyList<string>> index) {
        public IReadOnlyList<string> Flattened => [.. index.Values.SelectMany(static values => values).Distinct()];

        public bool ContainsAnyOf(IReadOnlyCollection<string> candidates) =>
            candidates.Any(candidate => index.ContainsKey(candidate) && index[candidate].Count > 0);

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> Empty =>
            new Dictionary<string, IReadOnlyList<string>>();
    }
}
