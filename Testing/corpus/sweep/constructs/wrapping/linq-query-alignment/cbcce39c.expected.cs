// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
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
