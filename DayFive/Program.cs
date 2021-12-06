using Common;
using Superpower;
using Superpower.Parsers;

var pairParser = Numerics.IntegerInt32.Then(v1 => Character.EqualTo(',').IgnoreThen(Numerics.IntegerInt32.Select(v2 => (v1, v2))));
var singleLineParser = pairParser
    .Then(v1 => Span.WhiteSpace
        .IgnoreThen(Span.EqualTo("->"))
        .IgnoreThen(Span.WhiteSpace)
        .IgnoreThen(pairParser)
        .Select(v2 => Line.FromCoordinates(v1, v2)));

new Runner().Run(
    singleLineParser.Try().ManyDelimitedBy(Span.WhiteSpace),
    lines =>
    {
        var board = new Board(lines.Max(l => Math.Max(l.Y1, l.Y2) + 1), lines.Max(l => Math.Max(l.X1, l.X2) + 1));

        foreach (var line in lines.Where(l => l.IsHorizontal || l.IsVertical))
        {
            board.Add(line);
        }

        return board.Total;
    },
    lines =>
    {
        var board = new Board(lines.Max(l => Math.Max(l.Y1, l.Y2) + 1), lines.Max(l => Math.Max(l.X1, l.X2) + 1));

        foreach (var line in lines)
        {
            board.Add(line);
        }

        return board.Total;
    });

public readonly record struct Line(int X1, int Y1, int X2, int Y2)
{
    public bool IsHorizontal => Y1 == Y2;

    public bool IsVertical => X1 == X2;

    public IEnumerable<(int x, int y)> Span
    {
        get
        {
            if (IsHorizontal)
            {
                return HorizontalSpan;
            }
            if (IsVertical)
            {
                return VerticalSpan;
            }
            return DiagonalSpan;
        }
    }

    private int HorizontalLength => Math.Abs(X1 - X2) + 1;

    private int VerticalLength => Math.Abs(Y1 - Y2) + 1;

    private IEnumerable<(int x, int y)> HorizontalSpan
    {
        get
        {
            var y = Y1;
            return Enumerable.Range(Math.Min(X1, X2), HorizontalLength).Select(x => (x, y));
        }
    }

    private IEnumerable<(int x, int y)> VerticalSpan
    {
        get
        {
            var x = X2;
            return Enumerable.Range(Math.Min(Y1, Y2), VerticalLength).Select(y => (x, y));
        }
    }

    private IEnumerable<(int x, int y)> DiagonalSpan
    {
        get
        {
            var deltaX = X1 <= X2 ? 1 : -1;
            var deltaY = Y1 <= Y2 ? 1 : -1;

            for (int i = 0, x = X1, y = Y1; i < HorizontalLength; i += 1, x += deltaX, y += deltaY)
            {
                yield return (x, y);
            }
        }
    }

    public static Line FromCoordinates((int, int) v1, (int, int) v2)
    {
        var ((x1, y1), (x2, y2)) = (v1, v2);

        return new Line(x1, y1, x2, y2);
    }
}

public sealed class Board
{
    public int RowCount { get; }

    public int ColumnCount { get; }

    private readonly int[][] _counts;

    public int Total => _counts.Sum(r => r.Count(n => n >= 2));

    public Board(int rowCount, int columnCount)
    {
        _counts = new int[rowCount][];

        for (var i = 0; i < rowCount; i += 1)
        {
            _counts[i] = new int[columnCount];
        }
    }

    public void Add(Line line)
    {
        foreach (var (x, y) in line.Span)
        {
            _counts[y][x] += 1;
        }
    }
}