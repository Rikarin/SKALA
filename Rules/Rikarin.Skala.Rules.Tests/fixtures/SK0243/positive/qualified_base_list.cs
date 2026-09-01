using System.Collections.Generic;

sealed class Names : List<string> {
    public System.Collections.Generic.IEnumerable<string> Sorted => this;
}
