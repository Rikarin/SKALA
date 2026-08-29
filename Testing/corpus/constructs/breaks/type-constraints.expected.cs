// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class SameLine<T, U> where T : struct where U : class {
    void M<V>(V v) where V : notnull { }
}

class OwnLines<T, U>
    where T : struct
    where U : class { }
