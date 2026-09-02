// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class SameLine<T, U>
    where T : struct where U : class {
    void M<V>(V v)
        where V : notnull { }
}

class OwnLines<T, U>
    where T : struct
    where U : class { }
