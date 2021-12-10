using Common;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
    Character.In('(', '[', '{', '<', ')', ']', '}', '>')
        .Select(Token.FromCharacter)
        .AtLeastOnce()
        .Try()
        .AtLeastOnceDelimitedBy(Span.WhiteSpace),
    lines => lines.Select(l => Line.Parse(l)).Where(e => e is InvalidClosingTagError).Sum(e => e!.Score),
    lines => lines.Select(l => Line.Parse(l)).Where(e => e is UnexpectedEndOfLineError).Select(e => e!.Score).Median()
);

public static class MedianExtension
{
    public static long Median(this IEnumerable<long> xs)
    {
        var list = xs.OrderBy(x => x).ToList();

        if (list.Count % 2 == 0)
        {
            throw new InvalidOperationException("Task guarantees there will be an odd number of elements.");
        }
        else
        {
            return list[list.Count / 2];
        }
    }
}

public static class Line
{
    public static Error? Parse(ReadOnlySpan<Token> line)
    {
        var stack = new Stack<Token>();

        for (var i = 0; i < line.Length; i+=1)
        {
            var token = line[i];

            if (token.IsOpening)
            {
                stack.Push(token);
            }
            else if (stack.Any() && stack.Peek().Counterpart == token)
            {
                stack.Pop();
            }
            else
            {
                return new InvalidClosingTagError(i, token.Character);
            }
        }

        if (stack.Any())
        {
            return new UnexpectedEndOfLineError(line.Length, stack);
        }

        return null;
    }
}

public readonly record struct Token(bool IsOpening, char Character)
{
    private char CounterpartCharacter => Character switch
    {
        '(' => ')',
        '[' => ']',
        '{' => '}',
        '<' => '>',
        ')' => '(',
        ']' => '[',
        '}' => '{',
        '>' => '<',
        _ => throw new ArgumentOutOfRangeException(),
    };

    public Token Counterpart => new Token(!IsOpening, CounterpartCharacter);

    public static Token FromCharacter(char character) => new Token()
    {
        Character = character,
        IsOpening = character switch
        {
            '(' => true,
            '[' => true,
            '{' => true,
            '<' => true,
            ')' => false,
            ']' => false,
            '}' => false,
            '>' => false,
            _ => throw new ArgumentOutOfRangeException(nameof(character)),
        }
    };
}

public abstract record class Error(int Position)
{
    public abstract long Score { get; }
}

public record class InvalidClosingTagError : Error
{
    public char Character { get; init; }

    public override long Score => Character switch
    {
        ')' => 3,
        ']' => 57,
        '}' => 1197,
        '>' => 25137,
        _ => throw new ArgumentOutOfRangeException()
    };

    public InvalidClosingTagError(int Position, char character) : base(Position) => Character = character;
}

public record class UnexpectedEndOfLineError : Error
{
    public IReadOnlyList<Token> ExpectedCompletion { get; }

    public override long Score => ExpectedCompletion.Aggregate(0L, (a, t) => a * 5 + TokenValue(t));

    private static int TokenValue(Token token) => token.Character switch
    {
        ')' => 1,
        ']' => 2,
        '}' => 3,
        '>' => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(token))
    };

    public UnexpectedEndOfLineError(int Position, IEnumerable<Token> unmatchedTokens) : base(Position) => 
        ExpectedCompletion = unmatchedTokens.Select(t => t.Counterpart).ToList();
}