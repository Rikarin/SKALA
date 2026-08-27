using System.Collections.Generic;
using System.Linq;

public sealed class Partitioner {
    public void Partition(List<int> source, List<int> negatives) {
        foreach (var item in source) {
            if (item < 0) {
                negatives.Add(item);
            }
        }
    }
}
