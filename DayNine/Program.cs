using System.Collections;
using Common;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
       Character.Digit.Select(c => c - '0').Many().ManyDelimitedBy(Span.WhiteSpace).Select(xs => new Board(xs)),
       board => board.LowPoints.Select(c => c.Height + 1).Sum(),
       board => board.FindBasins()
        .OrderByDescending(b => b.Count)
        .Take(3)
        .Aggregate(1, (a, b) => a * b.Count)
    );

public sealed class Board
{
    private readonly Cell[][] _cells;

    public IEnumerable<Cell> Cells => _cells.SelectMany(c => c);

    public IEnumerable<Cell> LowPoints => 
        Cells.Where(c => c.AdjacentCells.All(c2 => c2.Height > c.Height));

    public Board(int[][] cells)
    {
        _cells = new Cell[cells.Length][];

        for (var y = 0; y < cells.Length; y++)
        {
            _cells[y] = new Cell[cells[y].Length];

            for (var x = 0; x < cells[y].Length; x++)
            {
                _cells[y][x] = new Cell(this, x, y, cells[y][x]);
            }
        }
    }

    public IEnumerable<Basin> FindBasins()
    {
        var stack = new Stack<Cell>();

        foreach (var origin in LowPoints)
        {
            stack.Push(origin);
            var basin = new Basin();

            while (stack.Any())
            {
                var cell = stack.Pop();
                basin.Add(cell);

                foreach (var x in cell.AdjacentCells.Where(c => c.Height < 9 && !basin.Contains(c)))
                {
                    stack.Push(x);
                }
            }

            yield return basin;
        }
    }

    public readonly record struct Cell
    {
        public int Height { get; }

        public int X { get; }

        public int Y { get; }

        public IEnumerable<Cell> AdjacentCells
        {
            get
            {
                foreach (var (dx, dy) in AdjacencyMap)
                {
                    var nx = X + dx;
                    var ny = Y + dy;

                    if (ny >= 0 && ny < _board._cells.Length &&
                        nx >= 0 && nx < _board._cells[ny].Length)
                    {
                        yield return _board._cells[ny][nx];
                    }
                }
            }
        }

        private readonly Board _board;

        private static readonly IReadOnlyList<(int dx, int dy)> AdjacencyMap = new[]
        {
            (1, 0),
            (-1, 0),
            (0, 1),
            (0, -1),
        };

        public Cell(Board board, int x, int y, int height) =>
            (_board, X, Y, Height) = (board, x, y, height);
    }

    public sealed class Basin : IReadOnlyCollection<Cell>
    {
        private readonly HashSet<Cell> _cells = new();

        public int Count => _cells.Count;

        public void Add(Cell cell) => _cells.Add(cell);

        public bool Contains(Cell cell) => _cells.Contains(cell);

        public IEnumerator<Cell> GetEnumerator() => _cells.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}