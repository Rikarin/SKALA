// Agreement about `params` is the point; the modifier itself is not the finding.
namespace Fixtures {
    abstract class Sink {
        public virtual void Accept(string name, params int[] values) { }
    }

    sealed class CountingSink : Sink {
        public override void Accept(string name, params int[] values) { }
    }
}
