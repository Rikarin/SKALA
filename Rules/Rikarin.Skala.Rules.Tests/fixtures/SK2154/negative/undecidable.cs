// ⚠ The false-positive class that decides this rule, and the answer to the issue's open question.
// `Comparer<T>.Default` for an unsealed `T` casts each *element* to `IComparable` at run time, so a
// `List<Animal>` holding a comparable `Dog` sorts correctly. A type parameter is substituted at every
// call site. `int?` sorts through a dedicated `NullableComparer` even though `Nullable<T>` itself
// implements neither interface. None of these is decidable from the declaration, so none is reported.
using System.Collections.Generic;
using System.Linq;

class Animal { }

sealed class Dog : Animal { }

class C {
    void OpenGeneric<T>(List<T> items) => items.Sort();

    IEnumerable<T> OrderedGeneric<T>(IEnumerable<T> items) => items.OrderBy(i => i);

    void Unsealed(List<Animal> animals) => animals.Sort();

    IEnumerable<Animal> OrderedUnsealed(IEnumerable<Animal> animals) => animals.OrderBy(a => a);

    void Objects(List<object> values) => values.Sort();

    void NullableInts(List<int?> values) => values.Sort();

    IEnumerable<int?> OrderedNullable(IEnumerable<int?> values) => values.OrderBy(v => v);
}
