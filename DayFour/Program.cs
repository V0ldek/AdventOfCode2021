using System.Collections.Immutable;
using Common;
using Superpower;
using Superpower.Parsers;

var boardRowParser = Span.WhiteSpace.Optional().IgnoreThen(Numerics.IntegerInt32).Repeat(Board.Size);
var boardParser = Span.WhiteSpace.Optional().IgnoreThen(boardRowParser).Repeat(Board.Size);

new Runner().Run(
    Numerics.IntegerInt32.AtLeastOnceDelimitedBy(Character.EqualToIgnoreCase(','))
        .Then(xs => Span.WhiteSpace.IgnoreThen(boardParser.Try()).AtLeastOnce()
            .Select(bs => (numbers: xs, boards: bs.Select(Board.FromArray)))),
    x =>
    {
        IEnumerable<Board> boards = x.boards;
        foreach (var number in x.numbers)
        {
            boards = boards.Select(b => b.MarkNumber(number)).ToList();
            var winningBoard = boards.Select(b => (Board?)b).SingleOrDefault(b => b!.Value.IsWinning);
            if (winningBoard.HasValue)
            {
                return winningBoard.Value.Score * number;
            }
        }

        throw new InvalidOperationException("No winners.");
    },
    x =>
    {
        IEnumerable<Board> boards = x.boards;
        foreach (var number in x.numbers)
        {
            var markedBoards = boards.Select(b => b.MarkNumber(number)).ToList();
           
            if (markedBoards.Count == 1 && markedBoards[0].IsWinning)
            {
                return markedBoards[0].Score * number;
            }

            boards = markedBoards.Where(b => !b.IsWinning);
        }

        throw new InvalidOperationException("No winners or simultaneous winners.");
    }
);

public readonly struct Board
{
    public const int Size = 5;

    private readonly int[][] _numbers;

    private readonly ImmutableArray<ImmutableArray<bool>> Marks { get; init; }

    private Board(int[][] numbers)
    {
        _numbers = numbers;
        var marksBuilder = ImmutableArray.CreateBuilder<ImmutableArray<bool>>(Size);
        for (var i = 0; i < Size; i += 1)
        {
            var rowBuilder = ImmutableArray.CreateBuilder<bool>(Size);
            rowBuilder.Count = Size;
            marksBuilder.Add(rowBuilder.ToImmutable());
        }
        Marks = marksBuilder.ToImmutable();
    }

    public static Board FromArray(int[][] array) => new Board(array);

    public bool IsWinning => IsAnyRowWinning || IsAnyColumnWinning;

    public int Score
    {
        get
        {
            var sum = 0;
            for (var i = 0; i < Size; i += 1)
            {
                for (var j = 0; j < Size; j += 1)
                {
                    if (!Marks[i][j])
                    {
                        sum += _numbers[i][j];
                    }
                }
            }

            return sum;
        }
    }

    private bool IsAnyRowWinning => Enumerable.Range(0, Size).Any(IsNthRowWinning);

    private bool IsAnyColumnWinning => Enumerable.Range(0, Size).Any(IsNthColumnWinning);

    private bool IsNthRowWinning(int n) => Marks[n].All(x => x);

    private bool IsNthColumnWinning(int n) => Marks.All(row => row[n]);

    public Board MarkNumber(int x)
    {
        var newMarks = Marks;
        for (var i = 0; i < Size; i += 1)
        {
            for (var j = 0; j < Size; j += 1)
            {
                if (_numbers[i][j] == x)
                {
                    newMarks = newMarks.SetItem(i, newMarks[i].SetItem(j, true));
                }
            }
        }

        return this with { Marks = newMarks };
    }
}