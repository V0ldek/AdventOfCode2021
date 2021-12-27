using Common;
using Superpower;
using Superpower.Parsers;
using System.Text;

var cellParser = Character.In('.', '>', 'v').Select(x => x switch
{
    '.' => Cell.Empty,
    '>' => Cell.Right,
    'v' => Cell.Down,
    _ => throw new ArgumentOutOfRangeException()
});

var rowParser = cellParser.AtLeastOnce();

new Runner().Run(
    rowParser.Try().AtLeastOnceDelimitedBy(Span.WhiteSpace),
    x =>
    {
        var state = new State(x);
        Console.WriteLine(state.Print());

        while (!state.IsFixpoint)
        {
            state.TakeStep();
        }

        return state.StepsTaken;
    }
);

public sealed class Toroidal<T>
{
    private readonly ClearableCell[,] _array;

    private int _lastClear;

    public int Height => _array.GetLength(0);

    public int Length => _array.GetLength(1);

    public T? this[int y, int x]
    {
        get
        {
            var (nx, ny) = Wrap(x, y);
            return _array[ny, nx].Get(_lastClear);
        }
        set
        {
            var (nx, ny) = Wrap(x, y);
            _array[ny, nx] = _array[ny, nx].Set(value, _lastClear + 1);
        }
    }

    public Toroidal(IList<IList<T>> array)
    {
        if (array.Count == 0 || array[0].Count == 0)
        {
            throw new ArgumentException("Array cannot be empty.", nameof(array));
        }
        _array = new ClearableCell[array.Count, array[0].Count];

        for (var y = 0; y < array.Count; y += 1)
        {
            for (var x = 0; x < array[0].Count; x += 1)
            {
                _array[y, x] = new ClearableCell().Set(array[y][x], 1);
            }
        }
    }

    public void Clear() => _lastClear += 1;

    private (int x, int y) Wrap(int x, int y)
    {
        var nx = x < 0 ? Length + (x % Length) : x;
        var ny = y < 0 ? Height + (y % Height) : y;
        nx %= Length;
        ny %= Height;

        return (nx, ny);
    }

    private readonly struct ClearableCell
    {
        private readonly T? _value;

        public int LastSet { get; }

        private ClearableCell(T? value, int lastSet) =>
            (_value, LastSet) = (value, lastSet);

        public T? Get(int lastClear) =>
            LastSet > lastClear ? _value : default;

        public ClearableCell Set(T? value, int setTime) =>
            new ClearableCell(value, setTime);
    }
}

public class State
{
    private Toroidal<Cell> _board;

    private Toroidal<Cell> _scratchBoard;

    public int StepsTaken { get; private set; }

    public bool IsFixpoint { get; private set; }

    public State(IList<IList<Cell>> array)
    {
        _board = new Toroidal<Cell>(array);
        _scratchBoard = new Toroidal<Cell>(array);
    }

    public void TakeStep()
    {
        var anyMoved = false;
        _scratchBoard.Clear();

        MoveAllRight(ref anyMoved);
        MoveAllDown(ref anyMoved);

        (_board, _scratchBoard) = (_scratchBoard, _board);

        StepsTaken += 1;
        IsFixpoint = !anyMoved;
    }

    public string Print()
    {
        var sb = new StringBuilder();

        for (var y = 0; y < _board.Height; y += 1)
        {
            for (var x = 0; x < _board.Length; x += 1)
            {
                sb.Append(_board[y, x] switch
                {
                    Cell.Empty => '.',
                    Cell.Right => '>',
                    Cell.Down => 'v',
                    _ => throw new InvalidOperationException()
                });
            }
        
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void MoveAllRight(ref bool anyMoved)
    {
        for (var y = 0; y < _board.Height; y += 1)
        {
            for (var x = 0; x < _board.Length; x += 1)
            {
                if (_board[y, x] == Cell.Right)
                {
                    if (_board[y, x + 1] == Cell.Empty)
                    {
                        anyMoved = true;
                        _scratchBoard[y, x] = Cell.Empty;
                        _scratchBoard[y, x + 1] = Cell.Right;
                    }
                    else
                    {
                        _scratchBoard[y, x] = Cell.Right;
                    }
                }
            }
        }
    }

    private void MoveAllDown(ref bool anyMoved)
    {
        for (var y = 0; y < _board.Height; y += 1)
        {
            for (var x = 0; x < _board.Length; x += 1)
            {
                if (_board[y, x] == Cell.Down)
                {
                    if (_board[y + 1, x] != Cell.Down && _scratchBoard[y + 1, x] == Cell.Empty)
                    {
                        anyMoved = true;
                        _scratchBoard[y, x] = Cell.Empty;
                        _scratchBoard[y + 1, x] = Cell.Down;
                    }
                    else
                    {
                        _scratchBoard[y, x] = Cell.Down;
                    }
                }
            }
        }
    }
}

public enum Cell
{
    Empty,
    Right,
    Down
}