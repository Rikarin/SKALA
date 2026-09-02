// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class QueryAlignment {
    void TooLong(int[] numbers) {
        var doubledPositives = from number in numbers
            where number > 0 && number < 100
            orderby number descending
            select number * 2;
    }

    void GroupIntoContinuation(int[] numbers) {
        var query = from number in numbers
            where number > 0
            group number by number % 3
            into bucket
            orderby bucket.Key descending
            select bucket.Key;
    }
}
