using System.Text;
using Common;
using Superpower;
using Superpower.Parsers;

var foldParser = Span.EqualTo("fold along ")
    .IgnoreThen(Character.In('x', 'y'))
    .Then(d => Character.EqualTo('=')
        .IgnoreThen(Numerics.IntegerInt32)
        .Select(n => new Fold(d == 'x' ? Along.X : Along.Y, n)));

var pointParser = Numerics.IntegerInt32
    .Then(x => Character.EqualTo(',')
        .IgnoreThen(Numerics.IntegerInt32)
        .Select(y => new Point(x, y)));

new Runner().Run(
    pointParser.Try().ManyDelimitedBy(Span.WhiteSpace)
        .Then(ps => Span.WhiteSpace.IgnoreThen(foldParser.Try().ManyDelimitedBy(Span.WhiteSpace))
            .Select(fs => (points: ps, folds: fs))),
    x => x.points.Select(p => p.FoldBy(x.folds.First())).Distinct().Count(),
    x => Display(x.points.Select(p => x.folds.Aggregate(p, (p, f) => p.FoldBy(f))))
);

string Display(IEnumerable<Point> source)
{
    var points = source.Distinct().ToList();

    var maxX = points.Max(p => p.X);
    var maxY = points.Max(p => p.Y);
    var dictionary = points.ToDictionary(p => (p.X, p.Y));
    var result = new StringBuilder((maxX + 2) * (maxY + 2));

    for (var y = 0; y <= maxY; y += 1)
    {
        for (var x = 0; x <= maxX; x += 1)
        {
            result.Append(dictionary.ContainsKey((x, y)) ? ' ' : '#');
        }
        result.AppendLine();
    }

    return result.ToString();
}

public readonly record struct Fold(Along Direction, int Coordinate)
{
    public Fold Flipped => Direction switch
    {
        Along.X => this with { Direction = Along.Y },
        Along.Y => this with { Direction = Along.X },
        _ => throw new ArgumentOutOfRangeException()
    };
}

public readonly record struct Point(int X, int Y)
{
    public Point Flipped => new Point(Y, X);

    public Point FoldBy(Fold fold)
    {
        if (fold.Direction == Along.Y)
        {
            return this.Flipped.FoldBy(fold.Flipped).Flipped;
        }

        if (fold.Coordinate >= X)
        {
            return this;
        }

        var distance = X - fold.Coordinate;

        return this with { X = X - 2 * distance };
    }
}

public enum Along
{
    X,
    Y
}