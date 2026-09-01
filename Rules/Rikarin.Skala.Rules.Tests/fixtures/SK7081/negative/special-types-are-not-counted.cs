// analyzer-option: dotnet_code_quality.SK7081.threshold = 1
// ⚠ The language's own vocabulary is not a dependency. `int`, `string`, `bool`, `object`, `void`,
// `decimal`, `IEnumerable<T>`, `IDisposable` and `Array` are all `SpecialType`s: counting them
// would add the same handful to every type in the repository and separate nothing. At the
// shallowest threshold the rule still says nothing here.
using System;
using System.Collections.Generic;

namespace Fixtures;

class Vocabulary {
    int count;

    string text = string.Empty;

    bool flag;

    decimal amount;

    object? anything;

    public IEnumerable<string> Lines => [];

    public void Consume(IDisposable resource, long ticks, double ratio, char letter) {
        var narrowed = (byte)count;
        Array.Empty<int>();
    }
}
