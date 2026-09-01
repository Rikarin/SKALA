namespace Contoso.Design;

public class Buffering {
    protected const int PageSize = 4096;

    public int Pages(int bytes) => (bytes / PageSize) + 1;
}
