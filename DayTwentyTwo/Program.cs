using System.Diagnostics;
using Common;
using Superpower;
using Superpower.Parsers;

var dimensionParser = from low in Numerics.IntegerInt32
                      from _ in Span.EqualTo("..")
                      from high in Numerics.IntegerInt32
                      select new Dimensions(low, high);

var updateParser = from keyword in Span.EqualTo("on").Try().Or(Span.EqualTo("off"))
                   from xs in Span.EqualTo(" x=").IgnoreThen(dimensionParser)
                   from ys in Span.EqualTo(",y=").IgnoreThen(dimensionParser)
                   from zs in Span.EqualTo(",z=").IgnoreThen(dimensionParser)
                   select (target: new Subspace(xs, ys, zs), update: keyword.EqualsValue("on"));

var examples = new[] { "data/example1", "data/example2", "data/example3" };

var configuration = new Configuration
{
    PartOneExamplePaths = examples,
    PartTwoExamplePaths = examples
};

new Runner(configuration).Run(
    updateParser.Try().ManyDelimitedBy(Span.WhiteSpace),
    updates =>
    {
        var dimensions = new Subspace(new(-50, 50), new(-50, 50), new(-50, 50));
        var limitedUpdates = updates
            .Where(x => x.target.Intersects(dimensions))
            .Select(x => (x.target.LimitTo(dimensions), x.update));

        CubeTree? tree = ProcessUpdates(limitedUpdates);
        return tree?.Total ?? 0;
    },
    updates =>
    {
        CubeTree? tree = ProcessUpdates(updates);
        return tree?.Total ?? 0;
    }
);

static CubeTree? ProcessUpdates(IEnumerable<(Subspace target, bool value)> updates)
{
    CubeTree? tree = null;
    foreach (var (target, value) in updates)
    {
        tree = tree switch
        {
            null => new CubeLeaf(target),
            _ => value ? tree.Add(target) : tree.Remove(target)
        };
    }

    return tree;
}

public abstract class CubeTree
{
    public virtual long Total { get; }

    public abstract CubeTree Add(Subspace target);

    public abstract CubeTree? Remove(Subspace target);
}

public class CubeInner : CubeTree
{
    private readonly CubeTree _left;

    private readonly CubeTree _right;

    private readonly Line _separator;

    public override long Total => _left.Total + _right.Total;

    public CubeInner(Line separator, CubeTree left, CubeTree right) => 
        (_separator, _left, _right) = (separator, left, right);

    public override CubeTree Add(Subspace target)
    {
        var (targetLeft, targetRight) = target.SplitAlong(_separator);

        var newLeft = _left;
        var newRight = _right;

        if (targetLeft is not null)
        {
            newLeft = _left.Add(targetLeft.Value);
        }
        if (targetRight is not null)
        {
            newRight = _right.Add(targetRight.Value);
        }

        return new CubeInner(_separator, newLeft, newRight);
    }

    public override CubeTree? Remove(Subspace target)
    {
        var (targetLeft, targetRight) = target.SplitAlong(_separator);

        var newLeft = _left;
        var newRight = _right;

        if (targetLeft is not null)
        {
            newLeft = _left.Remove(targetLeft.Value);
        }
        if (targetRight is not null)
        {
            newRight = _right.Remove(targetRight.Value);
        }

        return FromChildren(_separator, newLeft, newRight);
    }

    public static CubeTree? FromChildren(Line separator, CubeTree? left, CubeTree? right) => (left, right) switch
    {
        (null, _) => right,
        (_, null) => left,
        _ => new CubeInner(separator, left, right)
    };
}

public class CubeLeaf : CubeTree
{
    private readonly Subspace _subspace;

    public override long Total => _subspace.Size;

    public CubeLeaf(Subspace subspace) => _subspace = subspace;

    public override CubeTree Add(Subspace target)
    {
        if (target.Within(_subspace))
        {
            return this;
        }

        if (_subspace.Within(target))
        {
            return new CubeLeaf(target);
        }

        var line = _subspace.SeparateFrom(target);
        var (dimLeft, dimRight) = _subspace.SplitAlong(line);
        var (targetLeft, targetRight) = target.SplitAlong(line);

        // Vioaltion of those would mean we chose a separating line that does not in fact separate
        // the two subspaces, so there is a bug in the SeparateFrom or SplitAlong methods.
        Debug.Assert(targetLeft is not null || dimLeft is not null);
        Debug.Assert(targetRight is not null || dimRight is not null);

        var left = dimLeft is null ? new CubeLeaf(targetLeft!.Value) : new CubeLeaf(dimLeft.Value);
        var right = dimRight is null ? new CubeLeaf(targetRight!.Value) : new CubeLeaf(dimRight.Value);

        return CubeInner.FromChildren(line, left, right)!.Add(target);
    }

    public override CubeTree? Remove(Subspace target)
    {
        if (!_subspace.Intersects(target))
        {
            return this;
        }

        if (_subspace.Within(target))
        {
            return null;
        }

        var line = _subspace.SeparateFrom(target.LimitTo(_subspace));
        var (dimLeft, dimRight) = _subspace.SplitAlong(line);

        var left = dimLeft is null ? null : new CubeLeaf(dimLeft.Value);
        var right = dimRight is null ? null : new CubeLeaf(dimRight.Value);

        return CubeInner.FromChildren(line, left, right)!.Remove(target);
    }
}


public enum Axis
{
    X,
    Y,
    Z
}

public readonly record struct Line(Axis Axis, int Position)
{
}

public readonly record struct Subspace(Dimensions X, Dimensions Y, Dimensions Z)
{
    public long Size => (long) X.Size * Y.Size * Z.Size;

    public bool Intersects(in Subspace other) =>
        X.Intersects(other.X) && Y.Intersects(other.Y) && Z.Intersects(other.Z);

    public Subspace LimitTo(in Subspace other) =>
        new Subspace(X.LimitTo(other.X), Y.LimitTo(other.Y), Z.LimitTo(other.Z));

    public Line SeparateFrom(Subspace other)
    {
        if (other == this)
        {
            throw new ArgumentException("Cannot SeparateFrom itself.", nameof(other));
        }

        if (X != other.X)
        {
            return new Line(Axis.X, X.SeparateFrom(other.X));
        }
        if (Y != other.Y)
        {
            return new Line(Axis.Y, Y.SeparateFrom(other.Y));
        }

        // Since other != this, here Z != other.Z necessarily.
        return new Line(Axis.Z, Z.SeparateFrom(other.Z));
    }

    public (Subspace? left, Subspace? right) SplitAlong(in Line line) => line.Axis switch
    {
        Axis.X => SplitAlongX(line.Position),
        Axis.Y => SplitAlongY(line.Position),
        Axis.Z => SplitAlongZ(line.Position),
        _ => throw new ArgumentOutOfRangeException(nameof(line)),
    };

    private (Subspace? left, Subspace? right) SplitAlongX(int position)
    {
        var (leftDimension, rightDimension) = X.SplitAlong(position);
        Subspace? left = leftDimension == null ? null : new Subspace(leftDimension.Value, Y, Z);
        Subspace? right = rightDimension == null ? null : new Subspace(rightDimension.Value, Y, Z);

        return (left, right);
    }

    private (Subspace? left, Subspace? right) SplitAlongY(int position)
    {
        var (leftDimension, rightDimension) = Y.SplitAlong(position);
        Subspace? left = leftDimension == null ? null : new Subspace(X, leftDimension.Value, Z);
        Subspace? right = rightDimension == null ? null : new Subspace(X, rightDimension.Value, Z);

        return (left, right);
    }

    private (Subspace? left, Subspace? right) SplitAlongZ(int position)
    {
        var (leftDimension, rightDimension) = Z.SplitAlong(position);
        Subspace? left = leftDimension == null ? null : new Subspace(X, Y, leftDimension.Value);
        Subspace? right = rightDimension == null ? null : new Subspace(X, Y, rightDimension.Value);

        return (left, right);
    }

    public bool Within(in Subspace other) => 
        X.Within(other.X) && Y.Within(other.Y) && Z.Within(other.Z);

    public override string ToString() => $"x={X},y={Y},z={Z}";
}

public readonly record struct Dimensions(int Low, int High)
{
    public int Size => High - Low + 1;

    public bool Contains(int point) => Low <= point && High >= point;

    public bool Intersects(in Dimensions other) => 
        other.Contains(Low) || other.Contains(High) || other.Within(this);

    public Dimensions LimitTo(in Dimensions limit) => 
        new Dimensions(Math.Max(Low, limit.Low), Math.Min(High, limit.High));

    public int SeparateFrom(in Dimensions other)
    {
        if (this == other)
        {
            throw new ArgumentException("Cannot separate a Dimension from itself.", nameof(other));
        }

        if (!Intersects(other))
        {
            return Math.Min(High, other.High);
        }

        if (other.Low < Low && other.High >= Low)
        {
            return Low - 1;
        }

        if (other.Low <= High && other.High > High)
        {
            return High;
        }

        return other.SeparateFrom(this);
    }

    public (Dimensions? left, Dimensions? right) SplitAlong(int point)
    {
        Dimensions? left = Low <= point ? new Dimensions(Low, Math.Min(point, High)) : null;
        Dimensions? right = High >= point + 1 ? new Dimensions(Math.Max(Low, point + 1), High) : null;

        return (left, right);
    }

    public bool Within(in Dimensions other) => other.Low <= Low && other.High >= High;

    public override string ToString() => $"{Low}..{High}";
}