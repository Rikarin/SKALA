// The same defect in the other accessor: setting `Height` overwrites the width.
sealed class Bounds {
    int width;
    int height;

    public int Width {
        get { return width; }
        set { width = value; }
    }

    public int Height {
        get { return height; }
        set { width = value; }
    }
}
