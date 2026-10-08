// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A sole lambda argument whose body is a member access the property fill would break (#557): past the
// margin the arrow breaks or the body fills on the arrow's line by the measured line — the body's end on
// the continuation line, the parameter list's width and the lambda's column — and a simple lambda from
// column 21 always breaks its arrow. The fill is one level past the line it starts on: the arrow's while
// the arrow stays, the body's once it breaks. Among other arguments the list chops instead.

class C {
    void M() {
        Use(x => x.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo.Lima.Mike.November.Oscar.Papa
            .Quebec.Romeo
        );
        Usssssssssssssssssssssss(x =>
            x.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo.Lima.Mike.November
        );
        Usssssssssssssssssssssss(x =>
            x.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo.Lima.Mike.November.Oscar.Papa.Quebec
                .Romeo.Sierra
        );
        Usssssssssssssssssssssss((first) =>
            first.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo.Lima
        );
        Use((Tttttttttt first, U second) => first.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo
            .Lima.Mike.November
        );
        Use((Tttttttttttttttttttttttttttttttttttttttttt first, Uuuuuuuuuuuuuuuuuuu second) =>
            first.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot
        );
        vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv.Use(x =>
            x.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett
        );
        Use(
            a,
            x => x.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo.Lima.Mike.November.Oscar.Papa
                .Quebec.Romeo.Sierra
        );
        Use(x => x.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo.Lima.Mike.Nov);
    }

    int P =>
        Use(x => x.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo.Lima.Mike.November.Oscar.Papa
            .Quebec
        );
}
