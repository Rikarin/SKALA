// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
using System;
using System.Collections.Generic;

namespace Skala.Corpus.Arrangement;

// Every declaration here is one `var` must NOT take. The negative set is the measurement: a rule
// that converts these still passes the positive fixtures.
public class VarRefused {
    public void DeclaredTypeIsNotTheInitialiserType() {
        // ⚠ The one that matters. Converting this changes the static type of `items` and every
        // overload resolved through it.
        IEnumerable<int> items = new List<int>();
        IList<int> list = new List<int>();
        object boxed = new List<int>();
    }

    public void NoTypeToInfer() {
        string nothing = null;
        Func<int> lambda = () => 1;
        Action method = Run;
    }

    public void MoreThanOneDeclarator() {
        int a = 1, b = 2;
    }

    public void NoInitialiser() {
        int uninitialised;
        uninitialised = 3;
        Console.WriteLine(uninitialised);
    }

    public void Run() { }
}
