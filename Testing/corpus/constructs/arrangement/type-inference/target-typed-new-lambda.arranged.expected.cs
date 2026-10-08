// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaCleanup generated=2026-10-08
namespace Skala.Corpus.Arrangement;

// #524, on its own file, and no option is globbed to it.
//
// A `new` that a lambda returns is target-typed when the delegate's return type is fixed from outside
// the lambda — a parameter whose call still binds the same member, a field, an assignment. It is not
// when the type is read off the lambda's own body: `Over` has a `Func<Uri>` and a `Func<Version>`
// overload, `Generic` infers its type argument from the lambda, and `var` takes the lambda's natural
// type.
public class TargetTypedNewLambda {
    readonly Func<Uri> _field = () => new("x:y");

    public void Take(Func<Uri> make) { }

    public void Over(Func<Uri> make) { }

    public void Over(Func<Version> make) { }

    public void Generic<T>(Func<T> make) { }

    public void TakeAsync(Func<Task<Uri>> make) { }

    public void Lambdas() {
        Take(() => new("x:y"));
        Take(() => { return new("x:y"); });
        TakeAsync(async () => new("x:y"));
        Over(() => new Uri("x:y"));
        Generic(() => new Uri("x:y"));
        Generic<Uri>(() => new("x:y"));
        var inferred = () => new Uri("x:y");
        Console.WriteLine(inferred() + _field().ToString());
    }
}
