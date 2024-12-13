namespace TairitsuSora.Commands.UltimateTicTacToe;

public class Board
{
    public enum CellType { None, Circle, Cross, Tie }

    public readonly record struct Coords(int Rank, int File)
    {
        public override string ToString() => $"{(char)('a' + File)}{Rank + 1}";

        public static Coords FromString(ReadOnlySpan<char> value)
        {
            if (value.Length != 2)
                throw new ArgumentException("Coordinate representations should be of length 2");
            if (value[0] is < 'a' or > 'i')
                throw new ArgumentException($"Rank should be from a to i, but got {value[0]}");
            if (value[1] is < '1' or > '9')
                throw new ArgumentException($"File should be from 1 to 8, but got {value[1]}");
            return new Coords(value[1] - '1', value[0] - 'a');
        }

        public static Coords? TryFromString(ReadOnlySpan<char> value)
        {
            if (value.Length != 2 ||
                value[0] is < 'a' or > 'i' ||
                value[1] is < '1' or > '9') return null;
            return new Coords(value[1] - '1', value[0] - 'a');
        }
    }

    private CellType[,] _small = new CellType[9, 9];
    private CellType[,] _large = new CellType[3, 3];

    public Coords? PlayableSection { get; private set; }
    public CellType ActivePlayer { get; set; } = CellType.Cross;
    public CellType Result { get; private set; } = CellType.None;

    public CellType this[int rank, int file] => _small[rank, file];
    public CellType this[Coords coords] => _small[coords.Rank, coords.File];
    public CellType AtLargeBoard(int rank, int file) => _large[rank, file];

    public void PlayAt(Coords coords)
    {
        _small[coords.Rank, coords.File] = ActivePlayer;
        ActivePlayer = ActivePlayer == CellType.Cross ? CellType.Circle : CellType.Cross;

        int secRank = coords.Rank / 3, secFile = coords.File / 3;
        _large[secRank, secFile] = CheckSection(_small, secRank * 3, secFile * 3);
        Result = CheckSection(_large, 0, 0);

        int nextSecRank = coords.Rank % 3, nextSecFile = coords.File % 3;
        PlayableSection = _large[nextSecRank, nextSecFile] != CellType.None
            ? null
            : new Coords(nextSecRank, nextSecFile);
    }

    public bool PlayableAt(Coords coords)
    {
        if (_small[coords.Rank, coords.File] != CellType.None) return false;
        int secRank = coords.Rank / 3, secFile = coords.File / 3;
        if (_large[secRank, secFile] != CellType.None) return false;
        if (PlayableSection is null) return true;
        return new Coords(secRank, secFile) == PlayableSection;
    }

    private CellType CheckSection(CellType[,] grid, int rankOffset, int fileOffset)
    {
        for (int i = 0; i < 3; i++)
        {
            if (grid[rankOffset + i, fileOffset] == grid[rankOffset + i, fileOffset + 1] &&
                grid[rankOffset + i, fileOffset] == grid[rankOffset + i, fileOffset + 2] &&
                grid[rankOffset + i, fileOffset] is CellType.Circle or CellType.Cross)
                return grid[rankOffset + i, fileOffset];
            if (grid[rankOffset, fileOffset + i] == grid[rankOffset + 1, fileOffset + i] &&
                grid[rankOffset, fileOffset + i] == grid[rankOffset + 2, fileOffset + i] &&
                grid[rankOffset, fileOffset + i] is CellType.Circle or CellType.Cross)
                return grid[rankOffset, fileOffset + i];
        }
        if (grid[rankOffset, fileOffset] == grid[rankOffset + 1, fileOffset + 1] &&
            grid[rankOffset, fileOffset] == grid[rankOffset + 2, fileOffset + 2] &&
            grid[rankOffset, fileOffset] is CellType.Circle or CellType.Cross)
            return grid[rankOffset, fileOffset];
        if (grid[rankOffset + 2, fileOffset] == grid[rankOffset + 1, fileOffset + 1] &&
            grid[rankOffset + 2, fileOffset] == grid[rankOffset, fileOffset + 2] &&
            grid[rankOffset + 2, fileOffset] is CellType.Circle or CellType.Cross)
            return grid[rankOffset + 2, fileOffset];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (grid[rankOffset + i, fileOffset + j] == CellType.None)
                    return CellType.None;
        return CellType.Tie;
    }
}
