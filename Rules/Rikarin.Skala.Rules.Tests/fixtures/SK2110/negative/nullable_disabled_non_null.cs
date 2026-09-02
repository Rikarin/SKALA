// The disabled context is not itself the finding; the null constant is.
#nullable disable
namespace Fixtures {
    sealed class Shipment {
        public override string ToString() {
            return "shipment";
        }
    }
}
