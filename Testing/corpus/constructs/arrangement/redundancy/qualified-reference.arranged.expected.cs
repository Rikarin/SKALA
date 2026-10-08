// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaCleanup generated=2026-10-08
using System.Diagnostics;
using System.Text;

namespace Skala.Corpus.Arrangement.QualifiedReference {
    // #460 / SK-DIV-0073, at the export's skala_prefer_qualified_reference = false, and no option is
    // globbed to it: the sweep would flip the key to `true`, which Skala does not perform.
    //
    // Shortened: a namespace qualifier the simple name does not need, in every position a type is
    // written — and a `global::`. Left alone: a name whose namespace is not imported (no using is ever
    // added), a name that would bind to something else, and a documentation `cref`.
    /// <summary>See <see cref="System.Text.StringBuilder" />.</summary>
    [Obsolete("x")]
    public class Shortened : IDisposable {
        List<int>.Enumerator _enumerator;

        readonly StringBuilder _builder = new();

        Stream? _stream;

        public void Dispose() { }

        public string Describe<T>(object value) where T : IComparable<T> {
            try {
                if (value is StringBuilder builder) {
                    return builder.ToString();
                }

                var watch = Stopwatch.StartNew();
                Console.WriteLine(watch);
                var empty = default(StringBuilder);
                var kind = typeof(StringBuilder);
                var named = nameof(StringBuilder);
                var many = new StringBuilder[2];
                var list = (IList<int>)new List<int>();
                return named + kind + empty + many.Length + list.Count + _builder + _stream + _enumerator.Current;
            } catch (InvalidOperationException) {
                return string.Empty;
            }
        }
    }

    public class Kept {
        // No using for System.Text.RegularExpressions, so nothing to shorten to — and never
        // `RegularExpressions.Regex`, although `using System.Text;` is there.
        readonly System.Text.RegularExpressions.Regex _pattern = new("x");

        // Only `global::` goes: the namespace is not imported.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("a", "b")]
        public string Pattern() => _pattern.ToString();

        // `Timer` is ambiguous here only if both are imported; System.Timers is not, so the
        // System.Threading one shortens and this one keeps its namespace.
        readonly System.Timers.Timer? _timer;

        public string Timer() => _timer?.ToString() ?? string.Empty;
    }
}

namespace Skala.Corpus.Arrangement.QualifiedReference.Shadowing {
    // A type of the same name in an enclosing scope: the qualifier is what keeps it the framework's.
    public class StringBuilder { }

    public class Shadowed {
        readonly System.Text.StringBuilder _framework = new();

        readonly Shortened? _sibling;

        public string Describe() => _framework.ToString() + new StringBuilder() + _sibling;
    }
}
