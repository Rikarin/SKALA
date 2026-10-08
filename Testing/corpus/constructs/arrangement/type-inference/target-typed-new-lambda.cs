using System;
using System.Threading.Tasks;

namespace Skala.Corpus.Arrangement;

// #524, on its own file, and no option is globbed to it.
//
// A `new` that a lambda returns is target-typed when the delegate's return type is fixed from outside
// the lambda — a parameter whose call still binds the same member, a field, an assignment. It is not
// when the type is read off the lambda's own body: `Over` has a `Func<Uri>` and a `Func<Version>`
// overload, `Generic` infers its type argument from the lambda, and `var` takes the lambda's natural
// type.
public class TargetTypedNewLambda {
    readonly Func<Uri> _field = () => new Uri("x:y");

    public void Take(Func<Uri> make) { }

    public void Over(Func<Uri> make) { }

    public void Over(Func<Version> make) { }

    public void Generic<T>(Func<T> make) { }

    public void TakeAsync(Func<Task<Uri>> make) { }

    public void Lambdas() {
        Take(() => new Uri("x:y"));
        Take(() => { return new Uri("x:y"); });
        TakeAsync(async () => new Uri("x:y"));
        Over(() => new Uri("x:y"));
        Generic(() => new Uri("x:y"));
        Generic<Uri>(() => new Uri("x:y"));
        var inferred = () => new Uri("x:y");
        Console.WriteLine(inferred() + _field().ToString());
    }
}
