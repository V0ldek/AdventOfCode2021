using Common;
using Superpower;
using Superpower.Parsers;

var examples = Enumerable.Range(1, 15).Select(n => $"data/example{n}").ToArray();
var configuration = new Configuration
{
    PartOneExamplePaths = examples,
    PartTwoExamplePaths = examples,
};

new Runner(configuration).Run(
    Character.HexDigit.Many(),
    hex =>
    {
        var parseResult = Packet.ParseFromHex(hex);
        return VersionSumTraversal.Traverse(parseResult.Result);
    },
    hex =>
    {
        var parseResult = Packet.ParseFromHex(hex);
        return ExpressionValueTraversal.Traverse(parseResult.Result);
    }
);

public readonly ref struct ParseResult<T>
{
    public T Result { get; init; }

    public ReadOnlySpan<byte> Remaining { get; init; }
}

public static class VersionSumTraversal
{
    public static int Traverse(Packet packet) => packet.Version + packet switch
    {
        OperatorPacket { Subpackets: var subpackets } => subpackets.Sum(s => Traverse(s)),
        _ => 0,
    };
}

public static class ExpressionValueTraversal
{
    public static long Traverse(Packet packet) => packet switch
    {
        LiteralPacket { Value: var value } => value,
        SumOperatorPacket { Subpackets: var sub } => sub.Sum(p => Traverse(p)),
        ProductOperatorPacket { Subpackets: var sub } => sub.Aggregate(1L, (a, p) => a * Traverse(p)),
        MinimumOperatorPacket { Subpackets: var sub } => sub.Min(p => Traverse(p)),
        MaximumOperatorPacket { Subpackets: var sub } => sub.Max(p => Traverse(p)),
        GreaterThanOperatorPacket { Subpackets: var sub } => Traverse(sub[0]) > Traverse(sub[1]) ? 1 : 0,
        LessThanOperatorPacket { Subpackets: var sub } => Traverse(sub[0]) < Traverse(sub[1]) ? 1 : 0,
        EqualToOperatorPacket { Subpackets: var sub } => Traverse(sub[0]) == Traverse(sub[1]) ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(packet)),
    };
}

public record class Packet
{
    public int Version { get; private set; }

    protected Packet()
    {
    }

    public static ParseResult<Packet> ParseFromHex(IEnumerable<char> hexData)
    {
        var bits = hexData
            .SelectMany(HexToBinaryString)
            .Select(x => (byte)(x - '0'))
            .ToArray()
            .AsSpan();

        return ParseFromBinary(bits);
    }

    public static ParseResult<Packet> ParseFromBinary(ReadOnlySpan<byte> bits)
    {
        var version = BitsToInt(bits[0..3]);
        var type = BitsToInt(bits[3..6]);

        ParseResult<Packet> result = type switch
        {
            4 => LiteralPacket.ParseFromBinary(bits[6..]),
            0 => OperatorPacket.ParseFromBinary<SumOperatorPacket>(bits[6..]),
            1 => OperatorPacket.ParseFromBinary<ProductOperatorPacket>(bits[6..]),
            2 => OperatorPacket.ParseFromBinary<MinimumOperatorPacket>(bits[6..]),
            3 => OperatorPacket.ParseFromBinary<MaximumOperatorPacket>(bits[6..]),
            5 => OperatorPacket.ParseFromBinary<GreaterThanOperatorPacket>(bits[6..]),
            6 => OperatorPacket.ParseFromBinary<LessThanOperatorPacket>(bits[6..]),
            7 => OperatorPacket.ParseFromBinary<EqualToOperatorPacket>(bits[6..]),
            _ => throw new InvalidOperationException("There are no more 3-bit integers.")
        };

        result.Result.Version = version;
        return result;
    }

    protected static int BitsToInt(ReadOnlySpan<byte> bits)
    {
        var value = 0;

        foreach (var bit in bits)
        {
            value *= 2;
            value += bit;
        }

        return value;
    }

    private static string HexToBinaryString(char hex) => char.ToLower(hex) switch
    {
        '0' => "0000",
        '1' => "0001",
        '2' => "0010",
        '3' => "0011",
        '4' => "0100",
        '5' => "0101",
        '6' => "0110",
        '7' => "0111",
        '8' => "1000",
        '9' => "1001",
        'a' => "1010",
        'b' => "1011",
        'c' => "1100",
        'd' => "1101",
        'e' => "1110",
        'f' => "1111",
        _ => throw new ArgumentOutOfRangeException(nameof(hex))
    };
}

public record class LiteralPacket(long Value) : Packet
{
    public new static ParseResult<Packet> ParseFromBinary(ReadOnlySpan<byte> bits)
    {
        long value = 0;
        int i;
        byte control = 1;

        for (i = 0; control == 1; i += 5)
        {
            value *= 16;
            value += BitsToInt(bits[(i + 1)..(i + 5)]);
            control = bits[i];
        }

        return new ParseResult<Packet>
        {
            Result = new LiteralPacket(value),
            Remaining = bits[i..]
        };
    }
}

public record class OperatorPacket : Packet
{
    public IReadOnlyList<Packet> Subpackets { get; init; }

    protected OperatorPacket() => Subpackets = Array.Empty<Packet>();

    public static ParseResult<Packet> ParseFromBinary<TPacket>(ReadOnlySpan<byte> bits)
        where TPacket : OperatorPacket, new()
    {
        var subpacketsResult = bits[0] == 0 ? ParseSubpacketsWithLength(bits[1..]) : ParseSubpacketsWithCount(bits[1..]);

        return new ParseResult<Packet>
        {
            Result = new TPacket { Subpackets = subpacketsResult.Result },
            Remaining = subpacketsResult.Remaining
        };
    }

    private static ParseResult<IReadOnlyList<Packet>> ParseSubpacketsWithLength(ReadOnlySpan<byte> bits)
    {
        var subpacketsLength = BitsToInt(bits[0..15]);

        var remaining = bits[15..];
        var breakLength = remaining.Length - subpacketsLength;
        var subpackets = new List<Packet>();

        while (remaining.Length > breakLength)
        {
            var subpacketResult = Packet.ParseFromBinary(remaining);
            subpackets.Add(subpacketResult.Result);
            remaining = subpacketResult.Remaining;
        }

        return new ParseResult<IReadOnlyList<Packet>>
        {
            Result = subpackets,
            Remaining = remaining
        };
    }

    private static ParseResult<IReadOnlyList<Packet>> ParseSubpacketsWithCount(ReadOnlySpan<byte> bits)
    {
        var count = BitsToInt(bits[0..11]);

        var remaining = bits[11..];
        var subpackets = new List<Packet>();

        while (subpackets.Count < count)
        {
            var subpacketResult = Packet.ParseFromBinary(remaining);
            subpackets.Add(subpacketResult.Result);
            remaining = subpacketResult.Remaining;
        }

        return new ParseResult<IReadOnlyList<Packet>>
        {
            Result = subpackets,
            Remaining = remaining
        };
    }
}

public record class SumOperatorPacket : OperatorPacket { }

public record class ProductOperatorPacket : OperatorPacket { }

public record class MinimumOperatorPacket : OperatorPacket { }

public record class MaximumOperatorPacket : OperatorPacket { }

public record class GreaterThanOperatorPacket : OperatorPacket { }

public record class LessThanOperatorPacket : OperatorPacket { }

public record class EqualToOperatorPacket : OperatorPacket { }