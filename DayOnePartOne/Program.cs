using Common;
using Common.Parsers;
using MoreLinq;

new Runner().Run(
    Parse.Integer.LineSeparated(),
    values => values.Aggregate((cnt: 0, prev: (int?)null), (a, x) => (x > a.prev ? a.cnt + 1 : a.cnt, x)).cnt,
    values =>
        values.Window(3)
            .Select(x => x.Sum())
            .Aggregate((cnt: 0, prev: (int?)null), (a, x) => (x > a.prev ? a.cnt + 1 : a.cnt, x)).cnt);