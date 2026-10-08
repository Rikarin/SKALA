// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// ⚠ A creation with an initializer written on one line, after an `=` that does not fit: the oracle moves it
// down whole up to a limit of its own, and breaks its braces past it. The limit grows with `new X {`, shrinks
// with the head from the declarator's name through the `=`, and does not exist under a twelve-column head.
// Each pair is the last value that moves down and the first that does not. Issue #581.

class C {
    private Something vxxxxxxxxxxxxxxxxxxx = new Something {
        Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
    };

    private Something vxxxxxxxxxxxxxxxxxxx = new Something {
        Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
    };

    private Something vxxxxxxxxxxxxxxxxxxxxxxxxxxx =
        new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };

    private Something vxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something {
        Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
    };

    void M() {
        var vxxxxxxxxxxxxx =
            new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
        var vxxxxxxxxxxxxx = new Something {
            Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
        };
        var vxxxxxxxxxxxxxxxxxxxxxxxxxxxxx =
            new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
        var vxxxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something {
            Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
        };
        var vxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx =
            new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
        var vxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something {
            Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
        };
        var vxxxxxxxxxxxxxxx =
            new P { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
        var vxxxxxxxxxxxxxxx = new P {
            Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
        };
        var vxxxxxxxxxxxxxxx =
            new SomethingMuchLongerStill { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeee };
        var vxxxxxxxxxxxxxxxxxxxxxxxxxxx =
            new List<string> { "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" };
        var vxxxxxxxxxxxxxxxxxxxxxxxxxxx = new List<string> {
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
        };
        var vxxxxxxxxxxxxxxxxxxxxxxx =
            new { A = 1, B = "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" };
        var vxxxxxxxxxxxxxxxxxxxxxxx = new {
            A = 1, B = "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
        };
        vxxxxxxxxxxxxxxxxxxxxxxxxxxxxx =
            new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
        vxxxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something {
            Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
        };
        Something vxxxxxxxxxxxxxxxxxxxxxxx =
            new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
        Something vxxxxxxxxxxxxxxxxxxxxxxx = new Something {
            Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
        };
        var vxxxx = new SomeTypeWithALongName {
            FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 333
        };
        var vxxxxx =
            new SomeTypeWithALongName { FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 333 };
        vxxxxxxxx = new SomeTypeWithALongName {
            FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 33333
        };
        vxxxxxxxxx =
            new SomeTypeWithALongName { FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 33333 };
    }
}
