using System.Collections.Immutable;
using Common;
using MoreLinq;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
    Character.In('0', '1').Select(ToBit).AtLeastOnce().Try().AtLeastOnceDelimitedBy(Span.WhiteSpace),
    values =>
    {
        var bitwiseSum = GetBitwiseSum(values);
        
        var mostCommon = bitwiseSum.GetMostCommonBits();
        var leastCommon = bitwiseSum.GetLeastCommonBits();

        var gamma = BitsToInt(mostCommon);
        var epsilon = BitsToInt(leastCommon);

        return gamma * epsilon;
    },
    values =>
    {
        var oxygenBits = FilterBy((b, i) => b.GetMostCommonBitAt(i), values);
        var co2Bits = FilterBy((b, i) => b.GetLeastCommonBitAt(i), values);

        var oxygenValue = BitsToInt(oxygenBits);
        var co2Value = BitsToInt(co2Bits);

        return oxygenValue * co2Value;
    }
);

static BitwiseSum GetBitwiseSum(byte[][] values) => 
    values.Aggregate(BitwiseSum.Zero(values[0].Length), (a, x) => a.Add(x));

static byte[] FilterBy(Func<BitwiseSum, int, byte> filter, byte[][] values)
{
    var bitwiseSum = GetBitwiseSum(values);

    return Impl(values, bitwiseSum, 0, filter);

    static byte[] Impl(IEnumerable<byte[]> values, BitwiseSum bitwiseSum, int i, Func<BitwiseSum, int, byte> filter)
    {
        if (bitwiseSum.Count == 1)
        {
            return values.Single();
        }
        if (i == bitwiseSum.Length)
        {
            return values.First();
        }

        var bit = filter(bitwiseSum, i);

        var (remaining, removed) = values.Partition(x => x[i] == bit);

        var newBitwiseSum = removed.Aggregate(bitwiseSum, (a, x) => a.Remove(x));

        return Impl(remaining, newBitwiseSum, i + 1, filter);
    }
}

static byte ToBit(char x) => x switch
{
    '0' => 0,
    '1' => 1,
    _ => throw new ArgumentOutOfRangeException(nameof(x))
};

static int BitsToInt(IEnumerable<byte> bits)
{
    var result = 0;

    foreach (var bit in bits)
    {
        result <<= 1;
        result += bit;
    }

    return result;
}

public readonly struct BitwiseSum
{
    private ImmutableArray<int> Sums { get; init; }

    public int Count { get; private init; }

    public int Length => Sums.Length;

    private BitwiseSum(ImmutableArray<int> sums)
    {
        Sums = sums;
        Count = 0;
    }

    public static BitwiseSum Zero(int length)
    {
        var builder = ImmutableArray.CreateBuilder<int>(length);
        builder.Count = length;
        return new(builder.ToImmutable());
    }

    public BitwiseSum Add(ReadOnlySpan<byte> number) => 
        Mutate((x, y) => x + y, number) with { Count = Count + 1 };

    public BitwiseSum Remove(ReadOnlySpan<byte> number) => 
        Mutate((x, y) => x - y, number) with { Count = Count - 1 };

    public byte GetMostCommonBitAt(int i) => (byte)(Sums[i] >= (Count - Sums[i]) ? 1 : 0);

    public byte GetLeastCommonBitAt(int i) => (byte)(Sums[i] >= (Count - Sums[i]) ? 0 : 1);

    public IEnumerable<byte> GetMostCommonBits() =>
        Enumerable.Range(0, Sums.Length).Select(GetMostCommonBitAt);

    public IEnumerable<byte> GetLeastCommonBits() =>
        Enumerable.Range(0, Sums.Length).Select(GetLeastCommonBitAt);

    private BitwiseSum Mutate(Func<int, byte, int> mutation, ReadOnlySpan<byte> number)
    {
        if (Sums.Length != number.Length)
        {
            throw new ArgumentException($"Length mismatch, expected '{Sums.Length}', got '{number.Length}'.", nameof(number));
        }

        var builder = Sums.ToBuilder();

        for (var i = 0; i < number.Length; i += 1)
        {
            builder[i] = mutation(builder[i], number[i]);
        }

        return this with { Sums = builder.ToImmutable() };
    }
}