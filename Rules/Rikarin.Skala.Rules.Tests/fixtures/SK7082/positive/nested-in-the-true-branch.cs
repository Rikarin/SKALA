// A conditional in the `true` branch of another: the reader has to hold the outer answer while
// working out which inner one selects it, and the grouping is invisible without counting.
namespace Fixtures;

class Labels {
    public static string Describe(bool ready, bool loud) => ready ? loud ? "READY" : "ready" : "waiting";
}
