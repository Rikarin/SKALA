// A constructor is the shape where this reads most like diligence: the argument is validated
// somewhere else, replaced here, and the caller's value never reaches the field.
namespace Fixtures {
    sealed class Connection {
        readonly string endpoint;

        public Connection(string endpoint) {
            endpoint = "localhost:0";
            this.endpoint = endpoint;
        }

        public string Endpoint => endpoint;
    }
}
