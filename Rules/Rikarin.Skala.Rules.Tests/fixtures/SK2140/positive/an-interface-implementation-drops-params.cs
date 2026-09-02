// ⚠ Measured, and the opposite of what the issue proposing this rule assumed. An interface
// implementation does NOT inherit `params` from the interface, so the two references really do
// disagree: `((IPlain)sink).Accept("a", 1, 2)` compiles and `sink.Accept("a", 1, 2)` is CS1501,
// from the same object. Both halves of that were compiled and the errors read off the build.
//
// The override direction is the refuted one and lives in negative/an-override-cannot-change-params.
namespace Fixtures {
    interface IPlain {
        void Accept(string name, params int[] values);
    }

    sealed class PlainSink : IPlain {
        public void Accept(string name, int[] values) { }
    }
}
