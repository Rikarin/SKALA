// Every one of these is a legal identifier without the escape, so the escape is disambiguation.
class C {
    int M() {
        var @var = 1;
        var @record = 2;
        var @value = 3;
        var @async = 4;
        var @await = 5;
        var @dynamic = 6;
        return @var + @record + @value + @async + @await + @dynamic;
    }
}
