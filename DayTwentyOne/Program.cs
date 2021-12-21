using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Common;
using MoreLinq;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
    from _ in Span.EqualTo("Player 1 starting position: ")
    from p1 in Numerics.IntegerInt32
    from __ in Span.WhiteSpace.IgnoreThen(Span.EqualTo("Player 2 starting position: "))
    from p2 in Numerics.IntegerInt32
    select (new Position(p1), new Position(p2)),
    positions =>
    {
        var game = new DeterministicGame(100, 1000, positions.Item1, positions.Item2);
        game.PlayUntilEnd();

        Debug.Assert(game.State.GameEnded);
        return game.TimesDieRolled * game.State.Loser.Value.Score;
    },
    positions =>
    {
        var game = new QuantumGame(3, 21);
        var result = game.CalculateUniverses(positions.Item1, positions.Item2);

        return Math.Max(result.Player1Victories, result.Player2Victories);
    }
);

public sealed class DeterministicGame
{
    private readonly DeterministicDie _die;

    public GameState State { get; private set; }

    public int TimesDieRolled => _die.TimesRolled;

    public DeterministicGame(int dieSize, int winningScore, Position player1Position, Position player2Position)
    {
        _die = new DeterministicDie(dieSize);
        State = GameState.InitialFromPositions(winningScore, player1Position, player2Position);
    }

    public void PlayUntilEnd()
    {
        while (!State.GameEnded)
        {
            var sum = _die.Roll() + _die.Roll() + _die.Roll();
            State = State.Move(sum);
        }
    }
}

public sealed class QuantumGame
{
    private readonly int _winningScore;
    private readonly IReadOnlyList<(int sum, int count)> _possibleRolls;

    public QuantumGame(int dieSize, int winningScore)
    {
        _winningScore = winningScore;
        var singleRolls = Enumerable.Range(1, dieSize);

        _possibleRolls = singleRolls
            .Cartesian(singleRolls, (x, y) => x + y)
            .Cartesian(singleRolls, (xy, z) => xy + z)
            .GroupBy(x => x)
            .Select(g => (g.Key, g.Count()))
            .ToList();
    }

    public QuantumResult CalculateUniverses(Position player1Position, Position player2Position)
    {
        var initialGameState = GameState.InitialFromPositions(_winningScore, player1Position, player2Position);
        var simulation = new QuantumSimulation(initialGameState, _possibleRolls);

        return simulation.Outcomes();
    }

    public sealed class QuantumSimulation
    {
        private readonly GameState _initialGameState;
        private readonly IReadOnlyList<(int sum, int count)> _possibleRolls;

        private readonly Dictionary<GameState, QuantumResult> _universes = new();

        public QuantumSimulation(GameState initialGameState, IReadOnlyList<(int sum, int count)> possibleRolls) => 
            (_initialGameState, _possibleRolls) = (initialGameState, possibleRolls);

        public QuantumResult Outcomes() => Outcomes(_initialGameState);

        private QuantumResult Outcomes(in GameState gameState)
        {
            if (_universes.TryGetValue(gameState, out QuantumResult memoizedResult))
            {
                return memoizedResult;
            }

            if (gameState.GameEnded)
            {
                var deterministicResult = gameState.Player1Won ? new QuantumResult(1, 0) : new QuantumResult(0, 1);
                _universes.Add(gameState, deterministicResult);
                return deterministicResult;
            }

            var result = new QuantumResult();

            foreach (var (roll, count) in _possibleRolls)
            {
                var newState = gameState.Move(roll);
                var outcomes = Outcomes(newState);

                result += outcomes * count;
            }

            _universes.Add(gameState, result);
            return result;
        }
    }
}

public readonly record struct QuantumResult(long Player1Victories, long Player2Victories)
{
    public QuantumResult Add(QuantumResult other) => 
        new(Player1Victories + other.Player1Victories, Player2Victories + other.Player2Victories);

    public QuantumResult Times(long n) => new(Player1Victories * n, Player2Victories * n);

    public static QuantumResult operator +(QuantumResult lhs, QuantumResult rhs) => lhs.Add(rhs);

    public static QuantumResult operator *(QuantumResult lhs, long rhs) => lhs.Times(rhs);
}

public sealed class DeterministicDie
{
    private int _current;

    public int Size { get; }

    public int TimesRolled { get; private set; }

    public DeterministicDie(int size) => Size = size;

    public int Roll()
    {
        _current = _current % Size + 1;
        TimesRolled += 1;
        return _current;
    }
}

public readonly record struct GameState(int WinningScore, PlayerState Player1, PlayerState Player2) : IEquatable<GameState>
{
    public bool IsPlayer2Move { get; private init; } = false;

    [MemberNotNullWhen(true, nameof(Winner))]
    [MemberNotNullWhen(true, nameof(Loser))]
    public bool Player1Won => Player1.Score >= WinningScore;

    [MemberNotNullWhen(true, nameof(Winner))]
    [MemberNotNullWhen(true, nameof(Loser))]
    public bool Player2Won => Player2.Score >= WinningScore;

    [MemberNotNullWhen(true, nameof(Winner))]
    [MemberNotNullWhen(true, nameof(Loser))]
    public bool GameEnded => Player1Won || Player2Won;

    public PlayerState? Winner => Player1Won ? Player1 : Player2Won ? Player2 : null;

    public PlayerState? Loser => Player1Won ? Player2 : Player2Won ? Player1 : null;

    public static GameState InitialFromPositions(int winningScore, Position player1Position, Position player2Position) =>
        new GameState(winningScore, new() { Position = player1Position }, new() { Position = player2Position });

    public GameState Move(int squares)
    {
        if (GameEnded)
        {
            throw new InvalidOperationException("Cannot move after game ended.");
        }

        return IsPlayer2Move
            ? this with { Player2 = Player2.Move(squares), IsPlayer2Move = false }
            : this with { Player1 = Player1.Move(squares), IsPlayer2Move = true };
    }

    public override int GetHashCode() => HashCode.Combine(Player1, Player2);

    public bool Equals(in GameState other) => Player1 == other.Player1 && Player2 == other.Player2;
}

public readonly record struct PlayerState(Position Position, int Score)
{
    public PlayerState Move(int squares)
    {
        var newPosition = Position + squares;

        return new PlayerState(newPosition, Score + newPosition.Value);
    }
}

public readonly record struct Position
{
    private const int BoardSize = 10;

    private readonly int _value;

    public int Value => _value + 1;

    public Position(int value)
    {
        _value = value - 1;

        if (Value < 1 || Value > BoardSize)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public Position Add(int n) => FromRaw((_value + n) % BoardSize);

    public static Position operator +(in Position position, int n) => position.Add(n);

    private static Position FromRaw(int value) => new Position(value + 1);
}