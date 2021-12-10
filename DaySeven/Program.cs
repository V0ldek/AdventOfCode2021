using Common;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
    Numerics.IntegerInt32.ManyDelimitedBy(Character.EqualTo(',')),
    values =>
    {
        var (low, high) = Median(values);
        
        var forLow = values.Select(x => Math.Abs(low - x)).Sum();
        var forHigh = values.Select(x => Math.Abs(high - x)).Sum();

        return Math.Min(forLow, forHigh);
    },
    values =>
    {
        var average = values.Average();
        var low = (int)Math.Floor(average);
        var high = (int)Math.Ceiling(average);

        var forLow = values.Select(x => Math.Abs(low - x) * (Math.Abs(low - x) + 1) / 2).Sum();
        var forHigh = values.Select(x => Math.Abs(high - x) * (Math.Abs(high - x) + 1) / 2).Sum();

        return Math.Min(forLow, forHigh);
    });

static (int low, int high) Median(IEnumerable<int> xs)
{
    var list = xs.OrderBy(x => x).ToList();

    if (list.Count % 2 == 0)
    {
        return (list[list.Count / 2 - 1], list[list.Count / 2]);
    }
    else
    {
        return (list[list.Count / 2], list[list.Count / 2]);
    }
}