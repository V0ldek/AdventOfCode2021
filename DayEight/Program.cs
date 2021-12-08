using System.Collections;
using Common;
using Superpower;
using Superpower.Parsers;

var artifactParser = Span.WhiteSpace.Optional().IgnoreThen(Character.In('a', 'b', 'c', 'd', 'e', 'f', 'g').AtLeastOnce());
var delimiter = Span.WhiteSpace.Optional().IgnoreThen(Character.EqualTo('|'));

new Runner().Run(
    artifactParser.Repeat(10)
        .Then(xs => delimiter.IgnoreThen(artifactParser.Repeat(4))
            .Select(ys => (key: xs.Select(Artifact.Parse), code: ys.Select(Artifact.Parse))))
        .Try().ManyDelimitedBy(Span.WhiteSpace),
    entries => entries.Sum(e => e.code.Count(x => new[] { 2, 3, 4, 7 }.Contains(x.Count))),
    entries => entries.Sum(e =>
    {
        var cf = e.key.Single(x => x.Count == 2);
        var acf = e.key.Single(x => x.Count == 3);
        var bcdf = e.key.Single(x => x.Count == 4);
        var abcdefg = e.key.Single(x => x.Count == 7);

        var a = cf.Xor(acf);
        // The only digit with this property is 9 (abcdfg).
        var abcdfg = e.key.Single(x => x.Count == 6 && x.Xor(a).Xor(bcdf).Count == 1);
        
        var g = abcdfg.Xor(a).Xor(bcdf);
        // The only digit with this property is 0 (abcefg).
        var abcefg = e.key.Single(x => x.Count == 6 && x != abcdfg && x.Xor(a).Xor(g).Xor(cf).Count == 2);

        // The only remaining digit with six segments is 6 (abdefg).
        var abdefg = e.key.Single(x => x.Count == 6 && x != abcdfg && x != abcefg);

        // The only digit with this property is 3 (acdfg).
        var acdfg = e.key.Single(x => x.Count == 5 && x.Xor(a).Xor(g).Xor(cf).Count == 1);

        // The only digit with this property is 5 (abdfg).
        var abdfg = e.key.Single(x => x.Count == 5 && x != acdfg && x.Xor(abcdfg).Count == 1);

        // The only remaining digit with five segments is 2 (acdeg).
        var acdeg = e.key.Single(x => x.Count == 5 && x != acdfg && x != abdfg);

        var map = new Dictionary<Artifact, int>
        {
            {cf, 1},
            {acdeg, 2},
            {acdfg, 3},
            {bcdf, 4},
            {abdfg, 5},
            {abdefg, 6},
            {acf, 7},
            {abcdefg, 8},
            {abcdfg, 9},
            {abcefg, 0},
        };

        var decoded = e.code.Reverse().Select((x, i) => map[x] * (int)Math.Pow(10, i));

        return decoded.Sum();
    })
);

public readonly record struct Artifact : IReadOnlyCollection<Segment>, IEquatable<Artifact>
{
    private IReadOnlyList<Segment> Segments { get; init; }

    public static Artifact Parse(IEnumerable<char> chars)
    {
        var segments = chars.Select(c => c switch
        {
            'a' => Segment.A,
            'b' => Segment.B,
            'c' => Segment.C,
            'd' => Segment.D,
            'e' => Segment.E,
            'f' => Segment.F,
            'g' => Segment.G,
            _ => throw new ArgumentOutOfRangeException(nameof(chars), $"Unexpected character '{c}'.")
        });
        return new Artifact(segments);
    }

    public Artifact(IEnumerable<Segment> segments) =>
        Segments = segments.OrderBy(x => x).Distinct().ToList();

    public int Count => Segments.Count;

    public bool Equals(Artifact other) => Segments.SequenceEqual(other.Segments);

    public override int GetHashCode() => Segments.Aggregate(0, (h, x) => HashCode.Combine(h, x));

    public Artifact Xor(Artifact other) =>
        this with { Segments = Segments.Union(other.Segments).Except(Segments.Intersect(other.Segments)).ToList() };

    public IEnumerator<Segment> GetEnumerator() => Segments.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public enum Segment
{
    A,
    B,
    C,
    D,
    E,
    F,
    G,
}