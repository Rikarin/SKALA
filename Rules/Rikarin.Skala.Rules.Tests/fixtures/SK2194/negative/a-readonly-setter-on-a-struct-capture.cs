// A `readonly` setter does not write its receiver, so `readonly struct` accepts calling it on a
// capture.
namespace Fixtures {
    sealed class Sink {
        public int Last;
    }

    struct Port {
        readonly Sink sink;

        public Port(Sink sink) => this.sink = sink;

        public readonly int Value {
            get => sink.Last;
            set => sink.Last = value;
        }
    }

    struct Adapter(Port port) {
        public void Send(int value) => port.Value = value;

        public int Last => port.Value;
    }
}
