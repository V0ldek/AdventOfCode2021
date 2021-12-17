using System.Collections.Immutable;
using System.Text;
using Common;
using MoreLinq;
using Superpower;
using Superpower.Parsers;

var intervalParser = Numerics.IntegerInt32.Then(a => Span.EqualTo("..").IgnoreThen(Numerics.IntegerInt32).Select(b => (a, b)));

new Runner().Run(
    Span.EqualTo("target area: x=")
        .IgnoreThen(intervalParser)
        .Then(xs => Span.EqualTo(", y=")
            .IgnoreThen(intervalParser)
            .Select(ys => (xs, ys))),
    target =>
    {
        var vx = Trajectory.MinVXToHit(target.xs.a);
        var vy = Trajectory.MaxVYToHit(target.ys.a);
        return new Trajectory(vx, vy).MaxY;
    },
    target =>
    {
        var vxMin = Trajectory.MinVXToHit(target.xs.a);
        var vxMax = target.xs.b;

        var vyMin = target.ys.a;
        var vyMax = Trajectory.MaxVYToHit(target.ys.a);

        var velocities = MoreEnumerable.Sequence(vxMin, vxMax)
            .Cartesian(MoreEnumerable.Sequence(vyMin, vyMax), (x, y) => (x, y));

        return velocities.Select(v => new Trajectory(v.x, v.y))
            .Count(t => t.Hits(target.xs.a, target.xs.b, target.ys.a, target.ys.b));
    }
);

public readonly record struct Trajectory(int VX, int VY)
{
    /* Horizontal velocity falls to 0 after VX steps.
     * Solve for VX: Position(VX) = targetX
     * ------------------------------------
     * (VX^2 + VX)/2 = targetX
     * VX^2 + VX = 2targetX
     * VX^2 + VX - 2targetX = 0
     * Solve the quadratic equation:
     * delta = 1 + 8targetX
     * sol = (-1 + sqrt(1 + 8targetX)) / 2
     * ------------------------------------
     * The result will most likely be fractional. We want the velocity that will actually reach
     * the target, so take the ceiling.
     */
    public static int MinVXToHit(int targetX) => (int)Math.Ceiling((-1 + Math.Sqrt(1 + 8 * targetX)) / 2);

    /* First note that Position(2VY + 1) = 0 for any VY.
     * Solve for t: Position(t).y = 0
     * ------------------------------------
     * tVY - (t(t-1)/2) = 0
     * 2tVY - t^2 + t = 0
     * -t^2 + (2VY + 1)t = 0 // t > 0
     * -t + 2VY + 1 = 0
     * t = 2VY + 1
     * ------------------------------------
     * Clearly then, maximal velocity at 2VY + 2 can be at most targetY,
     * or we will miss the target. So that is also the maximal velocity.
     * Solve for VY: Position(2VY + 2) = targetY
     * ------------------------------------
     * (2VY + 2)VY - (2VY + 2)(2VY + 1)/2 = targetY
     * 2VY^2 + 2VY - (4VY^2 + 6VY + 2)/2 = targetY
     * 2VY^2 + 2VY - 2VY^2 - 3VY - 1 = targetY
     * -VY - 1 = targetY
     * VY = -targetY - 1
     * ------------------------------------
     */
    public static int MaxVYToHit(int targetY) => -targetY - 1;

    public (int x, int y) Position(int t) => t <= VX
        ? (t * VX - (t * (t - 1) / 2), t * VY - (t * (t - 1) / 2))
        : ((VX * VX + VX) / 2, t * VY - (t * (t - 1) / 2));

    // After VY steps vertical velocity falls to 0, so that is the peak.
    public int MaxY => Position(VY).y;

    public bool Hits(int xa, int xb, int ya, int yb)
    {
        var @this = this;
        return MoreEnumerable
            .GenerateByIndex(t => (t, pos: @this.Position(t)))
            .TakeWhile(x => x.t <= 2 * @this.VY + 1 || x.pos.y >= ya)
            .Any(x => x.pos.x >= xa && x.pos.x <= xb
                   && x.pos.y >= ya && x.pos.y <= yb);
    }
}

public class Plot
{
    private readonly ImmutableDictionary<(int x, int y), char> _display = ImmutableDictionary<(int x, int y), char>.Empty;

    public Plot() => _display = _display.Add((0, 0), 'S');

    public int MaxY => _display.Keys.Max(p => p.y);

    private Plot(ImmutableDictionary<(int x, int y), char> display) => _display = display;

    public Plot AddArea(int ax, int bx, int ay, int by)
    {
        var newDisplay = _display.ToBuilder();

        for (var x = ax; x <= bx; x += 1)
        {
            for (var y = ay; y <= by; y += 1)
            {
                newDisplay[(x, y)] = 'T';
            }
        }

        return new Plot(newDisplay.ToImmutable());
    }

    public Plot AddTrajectory(in Trajectory trajectory, int stepsToPlot)
    {
        var newDisplay = _display.ToBuilder();

        for (var t = 1; t <= stepsToPlot; t += 1)
        {
            newDisplay[trajectory.Position(t)] = '#';
        }

        return new Plot(newDisplay.ToImmutable());
    }

    public string Display()
    {
        var builder = new StringBuilder();
        var minX = _display.Keys.Min(p => p.x);
        var maxX = _display.Keys.Max(p => p.x);
        var minY = _display.Keys.Min(p => p.y);
        var maxY = _display.Keys.Max(p => p.y);

        for (var y = maxY; y >= minY; y -= 1)
        {
            for (var x = minX; x <= maxX; x += 1)
            {
                var @char = _display.GetValueOrDefault((x, y), '.');

                builder.Append(@char);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static void Interactive(((int a, int b) xs, (int a, int b) ys) target)
    {
        var plot = new Plot().AddArea(target.xs.a, target.xs.b, target.ys.a, target.ys.b);
        var vx = Trajectory.MinVXToHit(target.xs.a);
        var vy = Trajectory.MaxVYToHit(target.ys.a);
        var resolution = vy * 2;

        while (true)
        {
            var trajectory = new Trajectory(vx, vy);
            var trajectoryPlot = plot.AddTrajectory(trajectory, resolution);
            Console.Clear();
            Console.WriteLine(trajectoryPlot.Display());

            Console.WriteLine($"VX = {vx}, VY = {vy}, Res = {resolution}, Score = {trajectoryPlot.MaxY}");

            var key = Console.ReadKey(true);

            switch (char.ToUpper(key.KeyChar))
            {
                case 'W':
                    vy += 1;
                    break;
                case 'S':
                    vy -= 1;
                    break;
                case 'A':
                    vx -= 1;
                    break;
                case 'D':
                    vx += 1;
                    break;
                case '-':
                    resolution -= 1;
                    break;
                case '=':
                    resolution += 1;
                    break;
                default:
                    break;
            }
        }
    }
}