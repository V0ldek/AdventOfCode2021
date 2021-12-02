using Common;
using MoreLinq;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
    Numerics.IntegerInt32.Try().ManyDelimitedBy(Span.WhiteSpace),
    values => values.Aggregate((cnt: 0, prev: (int?)null), (a, x) => (x > a.prev ? a.cnt + 1 : a.cnt, x)).cnt,
    values =>
        values.Window(3)
            .Select(x => x.Sum())
            .Aggregate((cnt: 0, prev: (int?)null), (a, x) => (x > a.prev ? a.cnt + 1 : a.cnt, x)).cnt);