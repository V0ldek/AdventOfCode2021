using Common;
using Superpower;
using Superpower.Parsers;
using Priority_Queue;

var digitParser = Character.Digit.Select(x => x - '0');

new Runner().Run(
    digitParser.AtLeastOnce().Try().AtLeastOnceDelimitedBy(Span.WhiteSpace).Select(x => new Board(x)),
    board => CalculateShortestPath(board),
    board => CalculateShortestPath(new ExtendedBoard(board, 5))
);

int CalculateShortestPath(IBoard board)
{
    var dp = new Dictionary<(int x, int y), int>();
    var active = new SimplePriorityQueue<(int x, int y)>();
    dp.Add((0, 0), 0);
    active.Enqueue((0, 0), 0);

    while (active.Any())
    {
        var (x, y) = active.Dequeue();
        var sourceRisk = dp[(x, y)];

        foreach (var (nx, ny) in board.MoveFrom(x, y))
        {
            var newRisk = sourceRisk + board[nx, ny];
            if (!dp.TryGetValue((nx, ny), out var oldRisk) || oldRisk > newRisk)
            {
                dp[(nx, ny)] = newRisk;
                active.Enqueue((nx, ny), newRisk);
            }
        }
    }

    return dp[(board.Dimensions.x - 1, board.Dimensions.y - 1)];
}

public interface IBoard
{
    public (int x, int y) Dimensions { get; }

    int this[int x, int y] { get; }

    public IEnumerable<(int x, int y)> MoveFrom(int x, int y);
}

public abstract class BoardBase : IBoard
{
    private static readonly IReadOnlyList<(int dx, int dy)> Moves = new[]
    {
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1)
    };

    public IEnumerable<(int x, int y)> MoveFrom(int x, int y) =>
        Moves.Select(m => (x: x + m.dx, y: y + m.dy))
            .Where(m => m.y >= 0 && m.y < Dimensions.y && m.x >= 0 && m.x < Dimensions.x);

    public abstract (int x, int y) Dimensions { get; }

    public abstract int this[int x, int y] { get; }
}

public sealed class Board : BoardBase
{
    private readonly IReadOnlyList<IReadOnlyList<int>> _risk;

    public override int this[int x, int y] => _risk[y][x];

    public override (int x, int y) Dimensions => (_risk[^1].Count, _risk.Count);

    public Board(IEnumerable<IEnumerable<int>> riskLevels) =>
        _risk = riskLevels.Select(x => x.ToArray()).ToArray();
}

public sealed class ExtendedBoard : BoardBase
{
    private readonly Board _board;

    private readonly int _factor;

    public ExtendedBoard(Board baseBoard, int factor) => 
        (_board, _factor) = (baseBoard, factor);

    public override int this[int x, int y]
    {
        get
        {
            if (x >= Dimensions.x || y >= Dimensions.y)
            {
                throw new IndexOutOfRangeException();
            }

            var stretchX = x / _board.Dimensions.x;
            var stretchY = y / _board.Dimensions.y;
            var sourceX = x % _board.Dimensions.x;
            var sourceY = y % _board.Dimensions.y;

            return (_board[sourceX, sourceY] - 1 + stretchX + stretchY) % 9 + 1; 
        }
    }

    public override  (int x, int y) Dimensions => 
        (_board.Dimensions.x * _factor, _board.Dimensions.y * _factor);
}