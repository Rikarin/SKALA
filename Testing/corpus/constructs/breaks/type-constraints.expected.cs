// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
class SameLine<T, U> where T : struct where U : class {
    void M<V>(V v) where V : notnull { }
}

class OwnLines<T, U>
    where T : struct
    where U : class { }
