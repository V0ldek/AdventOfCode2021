using Common;
using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;
using System.Text;

new Runner().Run(
    MonadTokenizer.Tokenizer,
    MonadParser.Instruction.AtLeastOnce(),
    instructions => BruteForce(instructions, x => x.Max()),
    instructions => BruteForce(instructions, x => x.Min())
);

void Analyse(IEnumerable<Instruction> instructions)
{
    var variableNames = new[] { "w", "x", "y", "z" };
    var variables = variableNames.Select(n => new VariableExpression(n));
    var state = new VariableState(variables);

    foreach (var (instruction, i) in instructions.Select((x, i) => (x, i)))
    {
        if (instruction is InputInstruction)
        {
            Console.WriteLine($"Input instruction at {i}");

            foreach (var (variable, expression) in state.GetVariables())
            {
                Console.WriteLine($"{variable.Print()} = {expression.Print()}");
            }
            var z = state.GetVariables()[new VariableExpression("z")];

            foreach (var j in Enumerable.Range(0, 26))
            {
                Console.WriteLine($"z <- {j}");
                Console.WriteLine(z.Substitute(new Dictionary<VariableExpression, Expression>()
                    {
                        {new VariableExpression("z"), new IntegerExpression(j) }
                    }).Print());
            }
            Console.WriteLine();

            state = new VariableState(variables);
        }

        state.ApplyInstruction(instruction);
    }

    Console.WriteLine("State at end:");

    foreach (var (variable, expression) in state.GetVariables())
    {
        Console.WriteLine($"{variable.Print()} = {expression.Print()}");
    }
    var z1 = state.GetVariables()[new VariableExpression("z")];

    foreach (var i in Enumerable.Range(0, 26))
    {
        Console.WriteLine($"z <- {i}");
        Console.WriteLine(z1.Substitute(new Dictionary<VariableExpression, Expression>()
            {
                {new VariableExpression("z"), new IntegerExpression(i) }
            }).Print());
    }
}

long BruteForce(
    IEnumerable<Instruction> instructions,
    Func<IEnumerable<long>, long> selector)
{
    var possibleInputs = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    IEnumerable<(State key, long value)> states = new[]
    {
            (new State(0, 0, 0, 0), 0L)
    };

    foreach (var instruction in instructions)
    {
        if (instruction is InputInstruction inputInstruction)
        {
            states = from kvp in states
                     from input in possibleInputs
                     let newState = kvp.key.ExecuteInput(inputInstruction, input)
                     let newValue = kvp.value * 10 + input
                     group (newState, newValue) by newState into g
                     select (g.Key, selector(g.Select(x => x.newValue)));
        }
        else
        {
            states = from kvp in states
                     select (kvp.key.ExecuteNonInput(instruction), kvp.value);
        }
        var list = states.ToList();
        states = list;
    }

    var valid = states.Where(kvp => kvp.key.Z == 0);

    return selector(valid.Select(x => x.value));
}

public readonly record struct State(long W, long X, long Y, long Z)
{
    public State ExecuteNonInput(Instruction instruction) => instruction switch
    {
        AddInstruction { LeftOperand: VariableExpression { Name: var name }, RightOperand: var right } =>
            WriteTo(name, ValueOf(name) + ValueOf(right)),
        MultiplyInstruction { LeftOperand: VariableExpression { Name: var name }, RightOperand: var right } =>
            WriteTo(name, ValueOf(name) * ValueOf(right)),
        DivideInstruction { LeftOperand: VariableExpression { Name: var name }, RightOperand: var right } =>
            WriteTo(name, ValueOf(name) / ValueOf(right)),
        ModuloInstruction { LeftOperand: VariableExpression { Name: var name }, RightOperand: var right } =>
            WriteTo(name, ValueOf(name) % ValueOf(right)),
        EqualInstruction { LeftOperand: VariableExpression { Name: var name }, RightOperand: var right } =>
            WriteTo(name, ValueOf(name) == ValueOf(right) ? 1 : 0),
        InputInstruction => throw new InvalidOperationException(),
        _ => throw new ArgumentOutOfRangeException(nameof(instruction))
    };

    public State ExecuteInput(InputInstruction instruction, int input) => instruction.Operand switch
    {
        VariableExpression { Name: var name } => WriteTo(name, input),
        _ => throw new ArgumentOutOfRangeException(nameof(instruction))
    };

    private State WriteTo(string name, long value) => name switch
    {
        "w" => this with { W = value },
        "x" => this with { X = value },
        "y" => this with { Y = value },
        "z" => this with { Z = value % (26 * 26 * 26 * 26) },
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private long ValueOf(Expression expression) => expression switch
    {
        IntegerExpression { Value: var n } => n,
        VariableExpression { Name: var name } => ValueOf(name),
        _ => throw new ArgumentOutOfRangeException(nameof(expression))
    };

    private long ValueOf(string name) => name switch
    {
        "w" => W,
        "x" => X,
        "y" => Y,
        "z" => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}

public class VariableState
{
    private Dictionary<VariableExpression, Expression> _temporaries = new();

    private Dictionary<VariableExpression, VariableExpression> _variables = new();

    private readonly List<InputDigitExpression> _inputVariables = new();

    private readonly List<VariableExpression> _temporariesChronologically = new();

    public VariableState(IEnumerable<VariableExpression> variables)
    {
        foreach (var variable in variables)
        {
            var i = NewTemporary(variable.Name, variable);
            _variables.Add(variable, i);
        }
    }

    public void ApplyInstruction(Instruction instruction)
    {
        var (key, replacement) = instruction switch
        {
            InputInstruction { Operand: var op } => (op, (Expression)NewInputVariable()),
            AddInstruction { LeftOperand: var left, RightOperand: var right } =>
                (left, AddExpression.Create(left, right)),
            MultiplyInstruction { LeftOperand: var left, RightOperand: var right } =>
                (left, MultiplyExpression.Create(left, right)),
            DivideInstruction { LeftOperand: var left, RightOperand: var right } =>
                (left, DivideExpression.Create(left, right)),
            ModuloInstruction { LeftOperand: var left, RightOperand: var right } =>
                (left, ModuloExpression.Create(left, right)),
            EqualInstruction { LeftOperand: var left, RightOperand: var right } =>
                (left, EqualExpression.Create(left, right)),
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };

        foreach (var (variable, temporary) in _variables)
        {
            replacement = replacement.Replace(variable, temporary);
        }

        var currentKeyTemporary = _variables[(VariableExpression)key];

        foreach (var (variable, temporary) in _variables.ToList())
        {
            var newTemporary = NewTemporary(variable.Name, temporary.Replace(currentKeyTemporary, replacement));
            _variables[variable] = newTemporary;
        }
    }

    public IReadOnlyDictionary<VariableExpression, Expression> GetVariables() =>
        _variables.ToDictionary(
            kvp => kvp.Key,
            kvp => _temporaries[kvp.Value].Substitute(_temporaries));

    private InputDigitExpression NewInputVariable()
    {
        var name = $"i{_inputVariables.Count}";
        var expr = new InputDigitExpression(name);
        _inputVariables.Add(expr);

        return expr;
    }

    private VariableExpression NewTemporary(string name, Expression value)
    {
        var var = new VariableExpression($"{name}{_temporaries.Count}");
        _temporaries[var] = value;
        _temporariesChronologically.Add(var);

        return var;
    }

    public string Print()
    {
        var sb = new StringBuilder();

        foreach (var (variable, temporary) in _variables)
        {
            sb.AppendLine($"{variable.Name} is currently {temporary.Name}");
        }

        foreach (var (temporary, expression) in _temporaries)
        {
            sb.AppendLine($"{temporary.Name} = {expression.Print()}");
        }

        return sb.ToString();
    }
}

public enum MonadToken
{
    Inp,
    Add,
    Mul,
    Div,
    Mod,
    Eql,
    Var,
    Int
}

public static class MonadTokenizer
{
    public static readonly Tokenizer<MonadToken> Tokenizer =
        new TokenizerBuilder<MonadToken>()
            .Ignore(Span.WhiteSpace)
            .Match(Span.EqualTo("inp"), MonadToken.Inp)
            .Match(Span.EqualTo("add"), MonadToken.Add)
            .Match(Span.EqualTo("mul"), MonadToken.Mul)
            .Match(Span.EqualTo("div"), MonadToken.Div)
            .Match(Span.EqualTo("mod"), MonadToken.Mod)
            .Match(Span.EqualTo("eql"), MonadToken.Eql)
            .Match(Span.Regex("[a-z][A-Z0-9]*"), MonadToken.Var)
            .Match(Numerics.IntegerInt32, MonadToken.Int)
            .Build();
}

public class MonadParser
{
    private static readonly TokenListParser<MonadToken, (Expression left, Expression right)> Ops =
        from op1 in Superpower.Parse.Ref(() => Expression!)
        from op2 in Superpower.Parse.Ref(() => Expression!)
        select (op1, op2);

    public static readonly TokenListParser<MonadToken, Instruction> InputInstruction =
        from _ in Token.EqualTo(MonadToken.Inp)
        from op in Superpower.Parse.Ref(() => Expression!)
        select (Instruction)new InputInstruction(op);

    public static readonly TokenListParser<MonadToken, Instruction> AddInstruction =
        from _ in Token.EqualTo(MonadToken.Add)
        from ops in Ops
        select (Instruction)new AddInstruction(ops.left, ops.right);

    public static readonly TokenListParser<MonadToken, Instruction> MultiplyInstruction =
        from _ in Token.EqualTo(MonadToken.Mul)
        from ops in Ops
        select (Instruction)new MultiplyInstruction(ops.left, ops.right);

    public static readonly TokenListParser<MonadToken, Instruction> DivideInstruction =
        from _ in Token.EqualTo(MonadToken.Div)
        from ops in Ops
        select (Instruction)new DivideInstruction(ops.left, ops.right);

    public static readonly TokenListParser<MonadToken, Instruction> ModuloInstruction =
        from _ in Token.EqualTo(MonadToken.Mod)
        from ops in Ops
        select (Instruction)new ModuloInstruction(ops.left, ops.right);

    public static readonly TokenListParser<MonadToken, Instruction> EqualInstruction =
        from _ in Token.EqualTo(MonadToken.Eql)
        from ops in Ops
        select (Instruction)new EqualInstruction(ops.left, ops.right);

    public static readonly TokenListParser<MonadToken, Instruction> Instruction =
        InputInstruction
            .Or(AddInstruction)
            .Or(MultiplyInstruction)
            .Or(DivideInstruction)
            .Or(ModuloInstruction)
            .Or(EqualInstruction);

    public static readonly TokenListParser<MonadToken, Expression> VariableExpression =
        Token.EqualTo(MonadToken.Var)
            .Select(s => (Expression)new VariableExpression(s.ToStringValue()));

    public static readonly TokenListParser<MonadToken, Expression> IntegerExpression =
        Token.EqualTo(MonadToken.Int)
            .Apply(Numerics.IntegerInt32)
            .Select(n => (Expression)new IntegerExpression(n));

    public static readonly TokenListParser<MonadToken, Expression> Expression =
        VariableExpression.Or(IntegerExpression);
}

public readonly record struct Precedence(int Value) : IComparable<Precedence>
{
    /* Expression precedence list:
     * 3. Integer, Variable, InputDigit
     * 2. Modulo, Divide, Multiply
     * 1. Add,
     * 0. Equal
     */
    public static Precedence Max => new(3);

    public static Precedence Min => new(0);

    public Precedence OneUp => new(Value + 1);

    public static Precedence Of(Expression expression) => expression switch
    {
        IntegerExpression => new(3),
        VariableExpression => new(3),
        InputDigitExpression => new(3),
        ModuloExpression => new(2),
        DivideExpression => new(2),
        MultiplyExpression => new(2),
        AddExpression => new(1),
        EqualExpression => new(0),
        _ => throw new ArgumentOutOfRangeException(nameof(expression))
    };

    public static Precedence Of<T>() where T : Expression
    {
        var t = typeof(T);
        if (t == typeof(IntegerExpression)) return new(3);
        if (t == typeof(VariableExpression)) return new(3);
        if (t == typeof(InputDigitExpression)) return new(3);
        if (t == typeof(ModuloExpression)) return new(2);
        if (t == typeof(DivideExpression)) return new(2);
        if (t == typeof(MultiplyExpression)) return new(2);
        if (t == typeof(AddExpression)) return new(1);
        if (t == typeof(EqualExpression)) return new(0);

        throw new ArgumentOutOfRangeException();
    }

    public int CompareTo(Precedence other) => Value - other.Value;

    public static bool operator <(Precedence a, Precedence b) =>
        a.CompareTo(b) < 0;

    public static bool operator >(Precedence a, Precedence b) =>
        a.CompareTo(b) > 0;
}

public abstract record class Instruction
{
    public abstract string Print();
}

public abstract record class UnaryInstruction(Expression Operand) : Instruction;

public abstract record class BinaryInstruction(Expression LeftOperand, Expression RightOperand) : Instruction;

public record class InputInstruction(Expression Operand) : UnaryInstruction(Operand)
{
    public override string Print() => $"inp {Operand.Print()}";
}

public record class AddInstruction(Expression LeftOperand, Expression RightOperand)
    : BinaryInstruction(LeftOperand, RightOperand)
{
    public override string Print() => $"add {LeftOperand.Print()} {RightOperand.Print()}";
}

public record class MultiplyInstruction(Expression LeftOperand, Expression RightOperand)
    : BinaryInstruction(LeftOperand, RightOperand)
{
    public override string Print() => $"mul {LeftOperand.Print()} {RightOperand.Print()}";
}

public record class DivideInstruction(Expression LeftOperand, Expression RightOperand)
    : BinaryInstruction(LeftOperand, RightOperand)
{
    public override string Print() => $"div {LeftOperand.Print()} {RightOperand.Print()}";
}

public record class ModuloInstruction(Expression LeftOperand, Expression RightOperand)
    : BinaryInstruction(LeftOperand, RightOperand)
{
    public override string Print() => $"mod {LeftOperand.Print()} {RightOperand.Print()}";
}

public record class EqualInstruction(Expression LeftOperand, Expression RightOperand)
    : BinaryInstruction(LeftOperand, RightOperand)
{
    public override string Print() => $"eql {LeftOperand.Print()} {RightOperand.Print()}";
}

public abstract record class Expression
{
    public abstract int Height { get; }

    public abstract int Size { get; }

    public bool Simplified { get; protected set; }

    public abstract Expression Replace(Expression target, Expression replacement);

    public Expression Substitute(IReadOnlyDictionary<VariableExpression, Expression> substitution) =>
        Substitute(substitution, new Dictionary<VariableExpression, Expression>());

    public abstract Expression Substitute(
        IReadOnlyDictionary<VariableExpression, Expression> substitution,
        Dictionary<VariableExpression, Expression> cache);

    public string Print() => Print(new StringBuilder()).ToString();

    public StringBuilder ParenthesisedIfTooLowPrecedence(StringBuilder sb, Precedence precedence) =>
        Precedence.Of(this) < precedence ? PrintParenthesised(sb) : Print(sb);

    protected abstract StringBuilder Print(StringBuilder sb);

    protected StringBuilder PrintParenthesised(StringBuilder sb) =>
        Print(sb.Append('(')).Append(')');
}

public abstract record class BinaryExpression(Expression LeftOperand, Expression RightOperand) : Expression
{
    public override int Height { get; } = Math.Max(LeftOperand.Height, RightOperand.Height) + 1;

    public override int Size { get; } = LeftOperand.Size + RightOperand.Size + 1;

    public override Expression Replace(Expression target, Expression replacement)
    {
        if (target.Height == Height)
        {
            return target == this ? replacement : this;
        }
        else
        {
            var left = LeftOperand.Replace(target, replacement);
            var right = RightOperand.Replace(target, replacement);

            return BinaryCreate(left, right);
        }
    }

    public override Expression Substitute(
        IReadOnlyDictionary<VariableExpression, Expression> substitution,
        Dictionary<VariableExpression, Expression> cache) =>
        BinaryCreate(LeftOperand.Substitute(substitution, cache), RightOperand.Substitute(substitution, cache));

    protected abstract Expression BinaryCreate(Expression left, Expression right);
}

public record class AddExpression(Expression LeftOperand, Expression RightOperand)
        : BinaryExpression(LeftOperand, RightOperand)
{
    protected override Expression BinaryCreate(Expression left, Expression right) =>
        Create(left, right);

    public static Expression Create(Expression left, Expression right) => (left, right) switch
    {
        (IntegerExpression { Value: var n }, _) when n == 0 => right,
        (_, IntegerExpression { Value: var n }) when n == 0 => left,
        (IntegerExpression { Value: var n1 }, IntegerExpression { Value: var n2 }) =>
            new IntegerExpression(n1 + n2),
        (_, _) => new AddExpression(left, right),
    };

    protected override StringBuilder Print(StringBuilder sb)
    {
        var sb2 = LeftOperand.ParenthesisedIfTooLowPrecedence(sb, Precedence.Of<AddExpression>().OneUp)
            .Append(" + ");
        return RightOperand.ParenthesisedIfTooLowPrecedence(sb2, Precedence.Of<AddExpression>());
    }
}

public record class MultiplyExpression(Expression LeftOperand, Expression RightOperand)
        : BinaryExpression(LeftOperand, RightOperand)
{
    protected override Expression BinaryCreate(Expression left, Expression right) =>
        Create(left, right);

    public static Expression Create(Expression left, Expression right) => (left, right) switch
    {
        (IntegerExpression { Value: var n }, _) when n == 1 => right,
        (_, IntegerExpression { Value: var n }) when n == 1 => left,
        (IntegerExpression { Value: var n }, _) when n == 0 => new IntegerExpression(0),
        (_, IntegerExpression { Value: var n }) when n == 0 => new IntegerExpression(0),
        (IntegerExpression { Value: var n1 }, IntegerExpression { Value: var n2 }) =>
            new IntegerExpression(n1 * n2),
        (_, _) => new MultiplyExpression(left, right),
    };

    protected override StringBuilder Print(StringBuilder sb)
    {
        var sb2 = LeftOperand.ParenthesisedIfTooLowPrecedence(sb, Precedence.Of<MultiplyExpression>().OneUp)
            .Append(" * ");
        return RightOperand.ParenthesisedIfTooLowPrecedence(sb2, Precedence.Of<MultiplyExpression>());
    }
}

public record class DivideExpression(Expression LeftOperand, Expression RightOperand)
        : BinaryExpression(LeftOperand, RightOperand)
{
    protected override Expression BinaryCreate(Expression left, Expression right) =>
        Create(left, right);

    public static Expression Create(Expression left, Expression right) => (left, right) switch
    {
        (_, IntegerExpression { Value: var n }) when n == 1 => left,
        (IntegerExpression { Value: var n }, _) when n == 0 => new IntegerExpression(0),
        (IntegerExpression { Value: var n1 }, IntegerExpression { Value: var n2 }) =>
            new IntegerExpression(n1 / n2),
        (_, _) => new DivideExpression(left, right),
    };

    protected override StringBuilder Print(StringBuilder sb)
    {
        var sb2 = LeftOperand.ParenthesisedIfTooLowPrecedence(sb, Precedence.Of<DivideExpression>().OneUp)
            .Append(" / ");
        return RightOperand.ParenthesisedIfTooLowPrecedence(sb2, Precedence.Of<DivideExpression>());
    }
}

public record class ModuloExpression(Expression LeftOperand, Expression RightOperand)
        : BinaryExpression(LeftOperand, RightOperand)
{
    protected override Expression BinaryCreate(Expression left, Expression right) =>
        Create(left, right);

    public static Expression Create(Expression left, Expression right) => (left, right) switch
    {
        (_, IntegerExpression { Value: var n }) when n == 1 => new IntegerExpression(0),
        (IntegerExpression { Value: var n }, _) when n == 0 => new IntegerExpression(0),
        (IntegerExpression { Value: var n1 }, IntegerExpression { Value: var n2 }) =>
            new IntegerExpression(n1 % n2),
        (_, _) => new ModuloExpression(left, right),
    };

    protected override StringBuilder Print(StringBuilder sb)
    {
        var sb2 = LeftOperand.ParenthesisedIfTooLowPrecedence(sb, Precedence.Of<ModuloExpression>().OneUp)
            .Append(" mod ");
        return RightOperand.ParenthesisedIfTooLowPrecedence(sb2, Precedence.Of<ModuloExpression>().OneUp);
    }
}

public record class EqualExpression(Expression LeftOperand, Expression RightOperand)
        : BinaryExpression(LeftOperand, RightOperand)
{
    protected override Expression BinaryCreate(Expression left, Expression right) =>
        Create(left, right);

    public static Expression Create(Expression left, Expression right) => (left, right) switch
    {
        (IntegerExpression { Value: var n1 }, IntegerExpression { Value: var n2 }) =>
            new IntegerExpression(n1 == n2 ? 1 : 0),
        (InputDigitExpression, IntegerExpression { Value: var n }) when n > 9 || n <= 0 =>
            new IntegerExpression(0),
        (IntegerExpression { Value: var n }, InputDigitExpression) when n > 9 || n <= 0 =>
            new IntegerExpression(0),
        (_, _) when left == right => new IntegerExpression(1),
        (_, _) => new EqualExpression(left, right),
    };

    protected override StringBuilder Print(StringBuilder sb)
    {
        var sb2 = LeftOperand.ParenthesisedIfTooLowPrecedence(sb, Precedence.Of<EqualExpression>().OneUp)
            .Append(" == ");
        return RightOperand.ParenthesisedIfTooLowPrecedence(sb2, Precedence.Of<EqualExpression>().OneUp);
    }
}

public record class IntegerExpression(int Value) : Expression
{
    public override int Height => 0;

    public override int Size => 1;

    public override Expression Replace(Expression target, Expression replacement) =>
        target == this ? replacement : this;

    public override Expression Substitute(
        IReadOnlyDictionary<VariableExpression, Expression> substitution,
        Dictionary<VariableExpression, Expression> cache) => this;

    protected override StringBuilder Print(StringBuilder sb) =>
        sb.Append(Value);
}

public record class InputDigitExpression(string Name) : Expression
{
    public override int Height => 0;

    public override int Size => 1;

    public override Expression Replace(Expression target, Expression replacement) =>
        target == this ? replacement : this;

    public override Expression Substitute(
        IReadOnlyDictionary<VariableExpression, Expression> substitution,
        Dictionary<VariableExpression, Expression> cache) => this;

    protected override StringBuilder Print(StringBuilder sb) =>
        sb.Append(Name);
}

public record class VariableExpression(string Name) : Expression
{
    public override int Height => 0;

    public override int Size => 1;

    public override Expression Replace(Expression target, Expression replacement) =>
        target == this ? replacement : this;

    public override Expression Substitute(
        IReadOnlyDictionary<VariableExpression, Expression> substitution,
        Dictionary<VariableExpression, Expression> cache)
    {
        if (cache.TryGetValue(this, out var cached))
        {
            return cached;
        }
        if (substitution.TryGetValue(this, out var value))
        {
            var substituted = value.Substitute(substitution, cache);
            cache[this] = substituted;
            return substituted;
        }

        return this;
    }

    protected override StringBuilder Print(StringBuilder sb) =>
        sb.Append(Name);
}