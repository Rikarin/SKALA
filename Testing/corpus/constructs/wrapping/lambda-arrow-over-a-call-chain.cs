namespace Constructs.Wrapping;

// Issue #529 (SK-DIV-0332). A lambda whose body is a chain of calls breaks its arrow exactly when the
// whole chain then fits on the line below; where it does not, the arrow stays and the chain breaks.
public class LambdaArrowOverACallChain {
    void N() {
        Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(predicateValue));
        Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo).Where(predicateValueeeeeeeeeeeeeeeee).ToList());
        Use(first, x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaa).Where(predicateValue));
        Use((x, y) => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaa).Where(predicateValue));
        var r = items.Where(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaa).Any(predicateValue));
        var s = items.Where(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo).Any(predicateValue)).ToList();
        var u = items.Where(x => source.Select(alphaArgumentValueNumberOne).Any(predicateValue)).Select(y => y.Name).ToList(argumentttt);
        Func<int, bool> f = x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaa).Any(predicateValue);
        Use(alpha, x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gam).Any(predicateValue), beta);
        var w = items.Select(x => x.Children.Where(c => c.IsEnabled).Select(c => c.Name)).Where(names => names.Any()).ToList();
    }
}
