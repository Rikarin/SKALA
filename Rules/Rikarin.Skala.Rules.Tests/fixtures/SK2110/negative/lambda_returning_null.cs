// Same question one syntax further in: a lambda body's return is the lambda's.
namespace Fixtures {
    sealed class Queue {
        public override string? ToString() {
            System.Func<string?> pick = () => null;
            return pick() ?? "queue";
        }
    }
}
