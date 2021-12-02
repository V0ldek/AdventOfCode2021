using Common;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
        Span.NonWhiteSpace
            .Then(cmd => Character.WhiteSpace.IgnoreThen(Numerics.IntegerInt32).Select(n => (cmd: cmd.ToStringValue(), n)))
            .Try()
            .ManyDelimitedBy(Span.WhiteSpace),
        cmds => cmds.Aggregate(SimpleState.Initial, (state, x) => ExecuteCommand(state, x.cmd, x.n)).Product(),
        cmds => cmds.Aggregate(AimBasedState.Initial, (state, x) => ExecuteCommand(state, x.cmd, x.n)).Product()
    );

static T ExecuteCommand<T>(IState<T> state, string command, int commandParameter) where T: IState<T> => command switch
{
    "forward" => state.Forward(commandParameter),
    "up" => state.Up(commandParameter),
    "down" => state.Down(commandParameter),
    _ => throw new InvalidOperationException($"Unexpected command: {command}")
};

interface IState<T> where T : IState<T>
{
    public int Horizontal { get; }

    public int Depth { get; }

    public T Forward(int x);

    public T Up(int x);

    public T Down(int x);

    public long Product() => Horizontal * Depth;
}

readonly struct SimpleState : IState<SimpleState>
{
    public int Horizontal { get; private init; }

    public int Depth { get; private init; }

    public static SimpleState Initial => new() { Horizontal = 0, Depth = 0 };

    public SimpleState Forward(int x) => this with { Horizontal = Horizontal + x };

    public SimpleState Up(int x) => this with { Depth = Depth - x };

    public SimpleState Down(int x) => this with { Depth = Depth + x };

    public long Product() => Horizontal * Depth;
}

readonly struct AimBasedState : IState<AimBasedState>
{
    public int Horizontal { get; private init; }

    public int Depth { get; private init; }

    public int Aim { get; private init; }

    public static AimBasedState Initial => new() { Horizontal = 0, Depth = 0, Aim = 0 };

    public AimBasedState Forward(int x) => this with { Horizontal = Horizontal + x, Depth = Depth + x * Aim };

    public AimBasedState Up(int x) => this with { Aim = Aim - x };

    public AimBasedState Down(int x) => this with { Aim = Aim + x };

    public long Product() => Horizontal * Depth;
}