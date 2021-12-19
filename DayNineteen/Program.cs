using System.Diagnostics.CodeAnalysis;
using Common;
using Superpower;
using Superpower.Parsers;

using FluentAssertions;
using Xunit;

using static MoreLinq.Extensions.CartesianExtension;

var beaconParser = from x in Numerics.IntegerInt32
                   from _ in Character.EqualTo(',')
                   from y in Numerics.IntegerInt32
                   from __ in Character.EqualTo(',')
                   from z in Numerics.IntegerInt32
                   select new Vector3(x, y, z);

var scannerHeader = Span.WhiteSpace.Many()
                        .IgnoreThen(Span.EqualTo("--- scanner "))
                        .Try()
                        .IgnoreThen(Numerics.Integer)
                        .IgnoreThen(Span.EqualTo(" ---"))
                        .IgnoreThen(Span.WhiteSpace.Many());

new Runner().Run(
    scannerHeader.IgnoreThen(beaconParser.Try()
        .AtLeastOnceDelimitedBy(Span.WhiteSpace)
        .Select(vs => new Scanner(vs))
        .AtLeastOnceDelimitedBy(scannerHeader)
        .Try()),
    scanners =>
    {
        var absoluteScanners = GetAbsolutePositions(scanners);
        var beacons = new HashSet<Vector3>();

        foreach (var scanner in absoluteScanners)
        {
            beacons.UnionWith(scanner.Beacons);
        }
        
        return beacons.Count;
    },
    scanners =>
    {
        var absoluteScanners = GetAbsolutePositions(scanners);

        return absoluteScanners.Cartesian(absoluteScanners, (s1, s2) => s1.ManhattanDistanceFrom(s2)).Max();
    }
);

IReadOnlyList<AbsoluteScanner> GetAbsolutePositions(IEnumerable<Scanner> scanners)
{
    var scannerSet = scanners.ToHashSet();
    var absoluteScanners = new List<AbsoluteScanner>();
    var absoluteScannersIdx = 0;

    while (scannerSet.Any())
    {
        var firstScanner = scannerSet.First();
        scannerSet.Remove(firstScanner);

        var zero = AbsoluteScanner.AsAbsoluteZero(firstScanner);
        absoluteScanners.Add(zero);

        while (absoluteScannersIdx < absoluteScanners.Count)
        {
            var referenceScanner = absoluteScanners[absoluteScannersIdx];

            foreach (var scanner in scannerSet.ToList())
            {
                if (scanner.TryMakeAbsolute(referenceScanner, out var absolute))
                {
                    scannerSet.Remove(scanner);
                    absoluteScanners.Add(absolute);
                }
            }

            absoluteScannersIdx += 1;
        }
    }

    return absoluteScanners;
}

public readonly record struct Vector3(int X, int Y, int Z)
{
    public static Vector3 Zero => new();

    public static IEnumerable<Func<Vector3, Vector3>> Rotations
    {
        get
        {
            var topRotations = new Func<Vector3, Vector3>[]
            {
                v => new(v.X, v.Y, v.Z),
                v => new(v.X, -v.Y, -v.Z),
                v => new(-v.X, v.Y, -v.Z),
                v => new(-v.X, -v.Y, v.Z)
            };

            return topRotations.SelectMany(f => new Func<Vector3, Vector3>[]
            {
                v => f(new (v.X, v.Y, v.Z)),
                v => f(new (v.Y, v.Z, v.X)),
                v => f(new (v.Z, v.X, v.Y)),
                v => f(new (-v.X, -v.Z, -v.Y)),
                v => f(new (-v.Z, -v.Y, -v.X)),
                v => f(new (-v.Y, -v.X, -v.Z)),
            });
        }
    }

    public Vector3 RelativeTo(Vector3 other) => new(X - other.X, Y - other.Y, Z - other.Z);

    public int ManhattanDistanceFrom(Vector3 other)
    {
        var distance = this.RelativeTo(other);
        return Math.Abs(distance.X) + Math.Abs(distance.Y) + Math.Abs(distance.Z);
    }
}

public sealed class Scanner
{
    public IReadOnlySet<Vector3> Beacons { get; }

    public Scanner(IEnumerable<Vector3> beacons) => Beacons = beacons.ToHashSet();

    public bool TryMakeAbsolute(AbsoluteScanner referenceScanner, [NotNullWhen(true)] out AbsoluteScanner? absolute)
    {
        foreach (var absoluteBeacon in referenceScanner.Beacons)
        {
            foreach (var rotation in Vector3.Rotations)
            {
                foreach (var beacon in Beacons.Select(b => rotation(b)))
                {
                    var offset = beacon.RelativeTo(absoluteBeacon);

                    if (referenceScanner.Overlaps(Beacons.Select(v => rotation(v).RelativeTo(offset))))
                    {
                        absolute = new AbsoluteScanner(this, v => rotation(v).RelativeTo(offset));
                        return true;
                    }
                }
            }
        }

        absolute = null;
        return false;
    }
}

public sealed class AbsoluteScanner
{
    public Func<Vector3, Vector3> Transformation { get; }

    public Vector3 Position => Transformation(Vector3.Zero);

    public IReadOnlySet<Vector3> Beacons { get; }

    public AbsoluteScanner(Scanner scanner, Func<Vector3, Vector3> transformation)
    {
        Transformation = transformation;
        Beacons = scanner.Beacons.Select(b => transformation(b)).ToHashSet();
    }

    public static AbsoluteScanner AsAbsoluteZero(Scanner scanner) => new AbsoluteScanner(scanner, x => x);

    public bool Overlaps(IEnumerable<Vector3> beacons) => beacons.Count(Beacons.Contains) >= 12;

    public int ManhattanDistanceFrom(AbsoluteScanner other) => Position.ManhattanDistanceFrom(other.Position);
}

public class Tests
{
    public static TheoryData<Vector3, IReadOnlySet<Vector3>> ValidRotations
    {
        get
        {
            var initial = new Vector3(1, 2, 3);
            var rotations = new HashSet<Vector3>
            {
                new (3, 1, 2),
                new (3, -1, -2),
                new (3, 2, -1),
                new (3, -2, 1),
                new (-3, 1, -2),
                new (-3, -1, 2),
                new (-3, 2, 1),
                new (-3, -2, -1),
                new (2, 3, 1),
                new (-2, 3, -1),
                new (-1, 3, 2),
                new (1, 3, -2),
                new (2, -3, -1),
                new (-2, -3, 1),
                new (1, -3, 2),
                new (-1, -3, -2),
                new (2, -1, 3),
                new (-2, 1, 3),
                new (1, 2, 3),
                new (-1, -2, 3),
                new (-1, 2, -3),
                new (1, -2, -3),
                new (-2, -1, -3),
                new (2, 1, -3),
            };

            return new() { { initial, rotations } };
        }
    }

    [Theory]
    [MemberData(nameof(ValidRotations))]
    public void RotationTests(Vector3 initial, IReadOnlySet<Vector3> expectedRotations)
    {
        var rotations = Vector3.Rotations.Select(r => r(initial));

        rotations.Should().BeEquivalentTo(expectedRotations);
    }

    [Fact]
    public void TryMakeAbsoluteTest()
    {
        var scanner0 = new Scanner(new Vector3[]
        {
            new(404,-588,-901),
            new(528,-643,409),
            new(-838,591,734),
            new(390,-675,-793),
            new(-537,-823,-458),
            new(-485,-357,347),
            new(-345,-311,381),
            new(-661,-816,-575),
            new(-876,649,763),
            new(-618,-824,-621),
            new(553,345,-567),
            new(474,580,667),
            new(-447,-329,318),
            new(-584,868,-557),
            new(544,-627,-890),
            new(564,392,-477),
            new(455,729,728),
            new(-892,524,684),
            new(-689,845,-530),
            new(423,-701,434),
            new(7,-33,-71),
            new(630,319,-379),
            new(443,580,662),
            new(-789,900,-551),
            new(459,-707,401),
        });
        var scanner1 = new Scanner(new Vector3[]
        {
            new(686,422,578),
            new(605,423,415),
            new(515,917,-361),
            new(-336,658,858),
            new(95,138,22),
            new(-476,619,847),
            new(-340,-569,-846),
            new(567,-361,727),
            new(-460,603,-452),
            new(669,-402,600),
            new(729,430,532),
            new(-500,-761,534),
            new(-322,571,750),
            new(-466,-666,-811),
            new(-429,-592,574),
            new(-355,545,-477),
            new(703,-491,-529),
            new(-328,-685,520),
            new(413,935,-424),
            new(-391,539,-444),
            new(586,-435,557),
            new(-364,-763,-893),
            new(807,-499,-711),
            new(755,-354,-619),
            new(553,889,-390),
        });

        var absolute0 = AbsoluteScanner.AsAbsoluteZero(scanner0);
        var success = scanner1.TryMakeAbsolute(absolute0, out var absolute1);

        success.Should().BeTrue();

        var offset = absolute1!.Transformation(Vector3.Zero);
        offset.Should().Be(new Vector3(68, -1246, -43));
    }
}

// 1, 2, 3
// 
// -3  2  1
// -3 -2 -1
// -3 -1  2
// -3  1 -2
//
//  3  2 -1
//  3 -2  1
//  3  1  2
//  3 -1 -2
//
//  2 -3 -1
// -2 -3  1
//  1 -3  2
// -1 -3 -2
//
//  2  3  1
// -2  3 -1
// -1  3  2
//  1  3 -2
//
//  2  1 -3
// -2 -1 -3
// -1  2 -3
//  1 -2 -3
//
//  2 -1  3
// -2  1  3
//  1  2  3
// -1 -2  3
//
//
//
// -3  2  1
// -3 -2 -1
//  3  2 -1
//  3 -2  1
//
// -3 -1  2
// -3  1 -2
//  3  1  2
//  3 -1 -2
//
//  2  3  1
//  2 -3 -1
// -2  3 -1
// -2 -3  1
//
// -2 -1 -3
//  2  1 -3
//  2 -1  3
// -2  1  3
//
//  1  2  3
//  1 -2 -3
// -1  2 -3
// -1 -2  3
//
// -1 -3 -2
// -1  3  2
//  1 -3  2
//  1  3 -2