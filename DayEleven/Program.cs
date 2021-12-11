using Common;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
    Character.Digit.Select(c => c - '0').AtLeastOnce().Try().AtLeastOnceDelimitedBy(Span.WhiteSpace).Select(x => new Board(x)),
    x =>
    {
        x.MakeSteps(100);
        return x.Flashes;
    },
    x =>
    {
        while (!x.Synchronized)
        {
            x.MakeStep();
        }

        return x.StepsTaken;
    }
);

public sealed class Board
{
    private readonly Cell[][] _board;

    public int StepsTaken { get; private set; }

    public int Flashes { get; private set; }

    public bool Synchronized => _board.All(row => row.All(c => c.EnergyLevel == 0));

    private static readonly IEnumerable<(int dx, int dy)> EnergyPropagation = new[]
    {
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1),
        (1, 1),
        (-1, 1),
        (1, -1),
        (-1, -1),
    };

    public Board(int[][] board) =>
        _board = board.Select((row, i) => row.Select((e, j) => new Cell(j, i, e)).ToArray()).ToArray();

    public void MakeStep()
    {
        var flashStack = new Stack<Cell>();
        var visited = new HashSet<Cell>();

        for (var y = 0; y < _board.Length; y += 1)
        {
            for (var x = 0; x < _board[y].Length; x += 1)
            {
                _board[y][x] = _board[y][x].IncreaseEnergy();

                if (_board[y][x].WillFlash)
                {
                    visited.Add(_board[y][x]);
                    flashStack.Push(_board[y][x]);
                }
            }
        }

        while (flashStack.Any())
        {
            var cell = flashStack.Pop();
            Flashes += 1;

            foreach (var (x, y) in PropagateFrom(cell))
            {
                if (!visited.Contains(_board[y][x]))
                {
                    _board[y][x] = _board[y][x].IncreaseEnergy();

                    if (_board[y][x].WillFlash)
                    {
                        visited.Add(_board[y][x]);
                        flashStack.Push(_board[y][x]);
                    }
                }
            }
        }

        foreach (var cell in visited)
        {
            _board[cell.Y][cell.X] = cell.ZeroEnergy();
        }

        StepsTaken += 1;
    }

    public void MakeSteps(int steps)
    {
        for (var i = 0; i < steps; i += 1)
        {
            MakeStep();
        }
    }

    public override string ToString() => 
        string.Join('\n', _board.Select(row => string.Join("", row.Select(c => c.EnergyLevel))));

    private IEnumerable<(int x, int y)> PropagateFrom(Cell cell) => 
        EnergyPropagation
            .Select(m => (x: cell.X + m.dx, y: cell.Y + m.dy))
            .Where(m => m.y >= 0 && m.y < _board.Length && m.x >= 0 && m.x < _board[m.y].Length);
}

public readonly record struct Cell : IEquatable<Cell>
{
    public int X { get; }

    public int Y { get; }

    public int EnergyLevel { get; init; }

    public bool WillFlash => EnergyLevel == 10;

    public Cell(int x, int y, int energyLevel) => (X, Y, EnergyLevel) = (x, y, energyLevel);

    public Cell IncreaseEnergy() => this with { EnergyLevel = EnergyLevel + 1 };

    public Cell ZeroEnergy() => this with { EnergyLevel = 0 };

    public bool Equals(in Cell other) => (X, Y) == (other.X, other.Y);

    public override int GetHashCode() => HashCode.Combine(X, Y);
}