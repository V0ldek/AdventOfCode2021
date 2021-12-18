using Common;
using MoreLinq;
using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;

using FluentAssertions;
using Xunit;

new Runner().Run(
    SnailfishTokenizer.Tokenizer,
    SnailfishParser.Value.AtLeastOnce(),
    values =>
    {
        var sum = values.Skip(1).Aggregate(values.First(), (a, v) => ReduceTraversal.Reduce(a.Add(v)));
        return MagnituteTraversal.Traverse(sum);
    },
    values =>
    {
        var possibilities = values
            .Cartesian(values, (v1, v2) => (v1, v2))
            .Where(x => !ReferenceEquals(x.v1, x.v2))
            .Select(x => ReduceTraversal.Reduce(x.v1.Add(x.v2)));

        return possibilities.Max(v => MagnituteTraversal.Traverse(v));
    }
);

public static class MagnituteTraversal
{
    public static long Traverse(Value value) => value switch
    {
        LiteralValue { Value: var n } => n,
        PairValue { Left: var left, Right: var right } => 3 * Traverse(left) + 2 * Traverse(right),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

public static class ReduceTraversal
{
    public static Value Reduce(Value value)
    {
        Value result = value;
        ExplodeResult explodeResult;
        SplitResult? splitResult = null;

        do
        {
            explodeResult = Explode(result, 0);
            result = explodeResult.Value;

            if (!explodeResult.Exploded)
            {
                splitResult = Split(result);
                result = splitResult.Value.Value;
            }
        }
        while (explodeResult.Exploded || splitResult?.Split is true);

        return result;
    }

    private static ExplodeResult Explode(Value value, int nesting) => value switch
    {
        LiteralValue => new ExplodeResult { Value = value },
        PairValue { Left: LiteralValue left, Right: LiteralValue right} when nesting >= 4 => new ExplodeResult
        {
            LeftCarry = left.Value,
            RightCarry = right.Value,
            Value = new LiteralValue(0),
            Exploded = true
        },
        PairValue { Left: LiteralValue, Right: LiteralValue } => new ExplodeResult { Value = value },
        PairValue pair => ComplexExplode(pair, nesting),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static ExplodeResult ComplexExplode(PairValue value, int nesting)
    {
        var leftResult = Explode(value.Left, nesting + 1);

        if (leftResult.Exploded)
        {
            return new ExplodeResult
            {
                LeftCarry = leftResult.LeftCarry,
                RightCarry = 0,
                Value = new PairValue(leftResult.Value, AddRightCarry(value.Right, leftResult.RightCarry)),
                Exploded = true
            };
        }

        var rightResult = Explode(value.Right, nesting + 1);

        if (rightResult.Exploded)
        {
            return new ExplodeResult
            {
                LeftCarry = 0,
                RightCarry = rightResult.RightCarry,
                Value = new PairValue(AddLeftCarry(value.Left, rightResult.LeftCarry), rightResult.Value),
                Exploded = true
            };
        }

        return new ExplodeResult
        {
            Value = value
        };
    }

    private static Value AddLeftCarry(Value value, int carry) => value switch
    {
        LiteralValue { Value: var n } => new LiteralValue(n + carry),
        PairValue { Right: var right } pair => pair with { Right = AddLeftCarry(right, carry) },
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static Value AddRightCarry(Value value, int carry) => value switch
    {
        LiteralValue { Value: var n } => new LiteralValue(n + carry),
        PairValue { Left: var left } pair => pair with { Left = AddRightCarry(left, carry) },
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static SplitResult Split(Value value) => value switch
    {
        LiteralValue { Value: var n } when n >= 10 => new SplitResult
        {
            Value = new PairValue(new LiteralValue(n / 2), new LiteralValue((n + 1) / 2)),
            Split = true
        },
        LiteralValue => new SplitResult { Value = value },
        PairValue { Left: var left, Right: var right } => SplitPair(left, right),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static SplitResult SplitPair(Value left, Value right)
    {
        var leftResult = Split(left);

        if (leftResult.Split)
        {
            return new SplitResult
            {
                Value = new PairValue(leftResult.Value, right),
                Split = true
            };
        }

        var rightResult = Split(right);

        return new SplitResult
        {
            Value = new PairValue(left, rightResult.Value),
            Split = rightResult.Split
        };
    }

    private readonly struct ExplodeResult
    {
        public Value Value { get; init; }

        public int LeftCarry { get; init; }

        public int RightCarry { get; init; }

        public bool Exploded { get; init; }
    }

    private readonly struct SplitResult
    {
        public Value Value { get; init; }

        public bool Split { get; init; }
    }
}

public abstract record class Value
{
    public Value Add(Value other) =>
        new PairValue(this, other);
}

public sealed record class PairValue(Value Left, Value Right) : Value
{
    public override string ToString() => $"[{Left},{Right}]";
}

public sealed record class LiteralValue(int Value) : Value
{
    public override string ToString() => Value.ToString();
}

public enum SnailfishToken
{
    Opening,
    Closing,
    Literal
}

public static class SnailfishTokenizer
{
    public static readonly Tokenizer<SnailfishToken> Tokenizer =
        new TokenizerBuilder<SnailfishToken>()
            .Ignore(Span.WhiteSpace)
            .Ignore(Character.EqualTo(','))
            .Match(Character.EqualTo('['), SnailfishToken.Opening)
            .Match(Character.EqualTo(']'), SnailfishToken.Closing)
            .Match(Numerics.Natural, SnailfishToken.Literal)
            .Build();
}

public class SnailfishParser
{
    public static readonly TokenListParser<SnailfishToken, Value> Pair =
        from _ in Token.EqualTo(SnailfishToken.Opening)
        from left in Superpower.Parse.Ref(() => Value!)
        from right in Superpower.Parse.Ref(() => Value!)
        from __ in Token.EqualTo(SnailfishToken.Closing)
        select (Value)new PairValue(left, right);

    public static readonly TokenListParser<SnailfishToken, Value> Literal =
        Token.EqualTo(SnailfishToken.Literal)
            .Apply(Numerics.IntegerInt32)
            .Select(n => (Value)new LiteralValue(n));

    public static readonly TokenListParser<SnailfishToken, Value> Value =
        Pair.Or(Literal);

    public static Value Parse(string input) =>
        Value.Parse(SnailfishTokenizer.Tokenizer.Tokenize(input));
}

public class Tests
{
    [Theory]
    [InlineData("[9,1]", 29)]
    [InlineData("[[9,1],[1,9]]", 129)]
    [InlineData("[[1,2],[[3,4],5]]", 143)]
    [InlineData("[[[[0,7],4],[[7,8],[6,0]]],[8,1]]", 1384)]
    [InlineData("[[[[1,1],[2,2]],[3,3]],[4,4]]", 445)]
    [InlineData("[[[[3,0],[5,3]],[4,4]],[5,5]]", 791)]
    [InlineData("[[[[5,0],[7,4]],[5,5]],[6,6]]", 1137)]
    [InlineData("[[[[8,7],[7,7]],[[8,6],[7,7]]],[[[0,7],[6,6]],[8,7]]]", 3488)]
    [InlineData("[[[[6,6],[7,6]],[[7,7],[7,0]]],[[[7,7],[7,7]],[[7,8],[9,9]]]]", 4140)]
    public void MagnitudeTest(string input, long expectedMagnitude)
    {
        var value = SnailfishParser.Parse(input);
        var magnitude = MagnituteTraversal.Traverse(value);

        magnitude.Should().Be(expectedMagnitude);
    }

    [Theory]
    [InlineData("[[[[4,3],4],4],[7,[[8,4],9]]]", "[1,1]", "[[[[0,7],4],[[7,8],[6,0]]],[8,1]]")]
    [InlineData("[[[0,[4,5]],[0,0]],[[[4,5],[2,6]],[9,5]]]", "[7,[[[3,7],[4,3]],[[6,3],[8,8]]]]", "[[[[4,0],[5,4]],[[7,7],[6,0]]],[[8,[7,7]],[[7,9],[5,0]]]]")]
    [InlineData("[[[[6,7],[6,7]],[[7,7],[0,7]]],[[[8,7],[7,7]],[[8,8],[8,0]]]]", "[[[[2,4],7],[6,[0,5]]],[[[6,8],[2,8]],[[2,1],[4,5]]]]", "[[[[7,0],[7,7]],[[7,7],[7,8]]],[[[7,7],[8,8]],[[7,7],[8,7]]]]")]
    [InlineData("[[[[7,0],[7,7]],[[7,7],[7,8]]],[[[7,7],[8,8]],[[7,7],[8,7]]]]", "[7,[5,[[3,8],[1,4]]]]", "[[[[7,7],[7,8]],[[9,5],[8,7]]],[[[6,8],[0,8]],[[9,9],[9,0]]]]")]
    [InlineData("[[[[7,7],[7,8]],[[9,5],[8,7]]],[[[6,8],[0,8]],[[9,9],[9,0]]]]", "[[2,[2,2]],[8,[8,1]]]", "[[[[6,6],[6,6]],[[6,0],[6,7]]],[[[7,7],[8,9]],[8,[8,1]]]]")]
    [InlineData("[[[[6,6],[6,6]],[[6,0],[6,7]]],[[[7,7],[8,9]],[8,[8,1]]]]", "[2,9]", "[[[[6,6],[7,7]],[[0,7],[7,7]]],[[[5,5],[5,6]],9]]")]
    [InlineData("[[[[6,6],[7,7]],[[0,7],[7,7]]],[[[5,5],[5,6]],9]]", "[1,[[[9,3],9],[[9,0],[0,7]]]]", "[[[[7,8],[6,7]],[[6,8],[0,8]]],[[[7,7],[5,0]],[[5,5],[5,6]]]]")]
    [InlineData("[[[[7,8],[6,7]],[[6,8],[0,8]]],[[[7,7],[5,0]],[[5,5],[5,6]]]]", "[[[5,[7,4]],7],1]", "[[[[7,7],[7,7]],[[8,7],[8,7]]],[[[7,0],[7,7]],9]]")]
    [InlineData("[[[[7,7],[7,7]],[[8,7],[8,7]]],[[[7,0],[7,7]],9]]", "[[[[4,2],2],6],[8,7]]", "[[[[8,7],[7,7]],[[8,6],[7,7]]],[[[0,7],[6,6]],[8,7]]]")]
    public void AdditionTest(string lhs, string rhs, string expectedResult)
    {
        var lhsValue = SnailfishParser.Parse(lhs);
        var rhsValue = SnailfishParser.Parse(rhs);
        var expectedValue = SnailfishParser.Parse(expectedResult);
        var sum = lhsValue.Add(rhsValue);
        var actual = ReduceTraversal.Reduce(sum);

        actual.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("[10, 0]", "[[5, 5], 0]")]
    [InlineData("[15,[0,13]]]", "[[7,8],[0,[6,7]]]")]
    [InlineData("[[[[[4,3],4],4],[7,[[8,4],9]]],[1,1]]", "[[[[0,7],4],[[7,8],[6,0]]],[8,1]]")]
    public void ReduceTest(string input, string expectedResult)
    {
        var value = SnailfishParser.Parse(input);
        var expectedValue = SnailfishParser.Parse(expectedResult);
        var reduced = ReduceTraversal.Reduce(value);

        reduced.Should().Be(expectedValue);
    }
}