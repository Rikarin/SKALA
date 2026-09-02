// Everything with an ordering already. None of these is excluded by a name on a list — each is
// excluded because the interface walk finds `IComparable` or `IComparable<T>` on it.
using System;
using System.Collections.Generic;
using System.Linq;

enum Rank {
    Low,
    High
}

sealed class Version : IComparable<Version> {
    public int CompareTo(Version other) => 0;
}

sealed class Legacy : IComparable {
    public int CompareTo(object other) => 0;
}

class C {
    void Ints(List<int> values) => values.Sort();
    void Strings(List<string> values) => values.Sort();
    void Dates(List<DateTime> values) => values.Sort();
    void Guids(List<Guid> values) => values.Sort();
    void Ranks(List<Rank> values) => values.Sort();
    void Versions(List<Version> values) => values.Sort();
    void Legacies(List<Legacy> values) => values.Sort();
    void Tuples(List<(int A, int B)> values) => values.Sort();
    IEnumerable<Rank> ByRank(IEnumerable<Rank> values) => values.OrderBy(v => v);
    IEnumerable<string> ByText(IEnumerable<string> values) => values.OrderBy(v => v);
}
