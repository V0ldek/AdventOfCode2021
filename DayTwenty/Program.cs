using System.Collections.Immutable;
using System.Text;
using Common;
using Superpower;
using Superpower.Parsers;

var keyParser = Character.In('#', '.').Select(x => x == '#' ? (byte)1 : (byte)0).AtLeastOnce();
var arrayParser = keyParser.Try().AtLeastOnceDelimitedBy(Span.WhiteSpace);

new Runner().Run(
    keyParser.Then(key => Span.WhiteSpace.IgnoreThen(arrayParser).Select(a => (key: new BitKey(key), map: new BitMap(a)))),
    x => x.map.EnhanceTimes(2, x.key).CountBits(1),
    x => x.map.EnhanceTimes(50, x.key).CountBits(1)
);

public readonly record struct BitCount(int? Count)
{
    public bool IsNumber => Count.HasValue;

    public bool IsInfinity => !IsNumber;

    public static BitCount Infinity => new();

    public override string ToString() => Count?.ToString() ?? "infinity";
}

public sealed class BitMap
{
    private readonly ImmutableList<ImmutableList<byte>> _bitmap;

    private byte _infiniteBit = 0;

    public BitCount CountBits(byte bit) => 
        _infiniteBit == bit ? BitCount.Infinity : new BitCount(_bitmap.Sum(row => row.Count(b => b == bit)));
    
    public BitMap(byte[][] array) =>
        _bitmap = PadWith(ImmutableList.CreateRange(array.Select(x => ImmutableList.Create(x))), 0);

    private BitMap(IEnumerable<IEnumerable<byte>> array, byte infiniteBit)
    {
        _infiniteBit = infiniteBit;
        _bitmap = PadWith(ImmutableList.CreateRange(array.Select(x => ImmutableList.CreateRange(x))), infiniteBit);
    }

    public BitMap Enhance(BitKey bitKey)
    {
        var indices = Enumerable.Range(0, _bitmap.Count).Select(y =>
            (y, xs: Enumerable.Range(0, _bitmap[y].Count)));
        var slices = indices.AsParallel().AsOrdered().Select(i => i.xs.AsParallel().AsOrdered().Select(x => 
            Slice.FromList<ImmutableList<ImmutableList<byte>>, ImmutableList<byte>>(_bitmap, x, i.y, _infiniteBit)));
        var bytes = slices.Select(row => row.Select(r => bitKey[r]));

        var newInfiniteBit = _infiniteBit == 0 ? bitKey[0] : bitKey[(1 << 9) - 1];

        var result = new BitMap(bytes, newInfiniteBit);

        return result;
    }

    public BitMap EnhanceTimes(int n, BitKey bitKey) => 
        Enumerable.Range(0, n).Aggregate(this, (map, _) => map.Enhance(bitKey));

    public string Display()
    {
        var result = new StringBuilder();

        foreach (var row in _bitmap)
        {
            foreach (var b in row)
            {
                result.Append(b == 0 ? '.' : '#');
            }
            result.AppendLine();
        }

        return result.ToString();
    }

    private static ImmutableList<ImmutableList<byte>> PadWith(ImmutableList<ImmutableList<byte>> list, byte infiniteBit)
    {
        var emptyRow = ImmutableList.CreateRange(Enumerable.Repeat(infiniteBit, list[0].Count + 2));
        var paddedRows = ImmutableList.CreateRange(list.Select(l => l.Insert(0, infiniteBit).Add(infiniteBit)));

        return paddedRows.Insert(0, emptyRow).Add(emptyRow);
    }
}

public sealed class BitKey
{
    private readonly IReadOnlyList<byte> _key;

    public BitKey(IEnumerable<byte> key) => _key = key.ToList();

    public byte this[int i] => _key[i];

    public byte this[Slice s] => this[s.AsInt32()];
}

public readonly struct Slice
{
    private readonly int _index;

    private static readonly IReadOnlyList<(int dx, int dy)> Ordering = new[]
    {
        (-1, -1),
        (0, -1),
        (1, -1),
        (-1, 0),
        (0, 0),
        (1, 0),
        (-1, 1),
        (0, 1),
        (1, 1),
    };

    private Slice(int index) => _index = index;

    public static Slice FromList<T, U>(T array, int indexX, int indexY, byte infiniteBit)
        where T : IReadOnlyList<U>
        where U : IReadOnlyList<byte>
    {
        var wrapped = InfiniteWrapper.Of<T, U>(array, infiniteBit);
        var index = 0;

        foreach (var (dx, dy) in Ordering)
        {
            index *= 2;
            index += wrapped[indexX + dx, indexY + dy];
        }

        return new Slice(index);
    }

    public int AsInt32() => _index;

    private readonly struct InfiniteWrapper<T, U>
        where T : IReadOnlyList<U>
        where U : IReadOnlyList<byte>
    {
        private readonly T _wrapped;

        private readonly byte _infiniteBit;

        public byte this[int x, int y] =>
            y < 0 || y >= _wrapped.Count || x < 0 || x >= _wrapped[y].Count
                ? _infiniteBit
                : _wrapped[y][x];

        public InfiniteWrapper(T array, byte infiniteBit) =>
            (_wrapped, _infiniteBit) = (array, infiniteBit);
    }

    private static class InfiniteWrapper
    {
        public static InfiniteWrapper<T, U> Of<T, U>(T array, byte infiniteBit)
            where T : IReadOnlyList<U>
            where U : IReadOnlyList<byte> => new InfiniteWrapper<T, U>(array, infiniteBit);
    }
}