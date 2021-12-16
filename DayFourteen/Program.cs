using Common;
using Superpower;
using Superpower.Parsers;
using static MoreLinq.Extensions.WindowExtension;

var ruleParser = Character.Letter.Repeat(2)
    .Then(x => Span.WhiteSpace.IgnoreThen(Span.EqualTo("->")).IgnoreThen(Span.WhiteSpace)
        .IgnoreThen(Character.Letter)
        .Select(y => new Rule(Pair.FromList(x), y)));

new Runner().Run(
    Character.Letter.AtLeastOnce()
        .Then(x => Span.WhiteSpace.IgnoreMany()
            .Then(_ => ruleParser.Try().ManyDelimitedBy(Span.WhiteSpace)
            .Select(rs => new Polymer(x, rs)))),
    template =>
    {
        var polymer = template.ApplyRulesNTimes(10);
        var elementCounts = polymer.GetElementCounts().ToList();

        return elementCounts.Max() - elementCounts.Min();
    },
    template =>
    {
        var polymer = template.ApplyRulesNTimes(40);
        var elementCounts = polymer.GetElementCounts().ToList();

        return elementCounts.Max() - elementCounts.Min();
    }
);

public class Polymer
{
    private readonly IDictionary<Pair, long> _pairs;

    private readonly IReadOnlyList<Rule> _rules;

    private const char Terminator = '\0';

    public IEnumerable<long> GetElementCounts()
    {
        var deconstructedPairs = _pairs.SelectMany(kvp => new[] 
        { 
            (element: kvp.Key.First, count: kvp.Value),
            (element: kvp.Key.Second, count: kvp.Value) 
        });

        return deconstructedPairs
            .Where(x => x.element != Terminator)
            .GroupBy(x => x.element)
            .Select(g => g.Sum(x => x.count) / 2);
    }

    public Polymer(IEnumerable<char> template, IEnumerable<Rule> rules)
    {
        var elements = template.Prepend(Terminator).Append(Terminator).ToList();

        _pairs = elements.Window(2).Select(xs => Pair.FromList(xs)).GroupBy(x => x).ToDictionary(x => x.Key, x => x.LongCount());
        _rules = rules.ToList();
    }

    private Polymer(IDictionary<Pair, long> pairs, IReadOnlyList<Rule> rules) =>
        (_pairs, _rules) = (pairs, rules);

    public Polymer ApplyRules()
    {
        var newPairs = new Dictionary<Pair, long>();

        foreach (var (pair, count) in _pairs)
        {
            var rule = _rules.Select(x => (Rule?)x).SingleOrDefault(r => r!.Value.From == pair);

            if (rule is null)
            {
                newPairs[pair] = count + newPairs.GetValueOrDefault(pair);
            }
            else 
            {
                var pair1 = new Pair(pair.First, rule.Value.To);
                var pair2 = new Pair(rule.Value.To, pair.Second);
                newPairs[pair1] = count + newPairs.GetValueOrDefault(pair1);
                newPairs[pair2] = count + newPairs.GetValueOrDefault(pair2);
            }
        }

        return new Polymer(newPairs, _rules);
    }

    public Polymer ApplyRulesNTimes(int n) =>
        Enumerable.Range(0, n).Aggregate(this, (p, _) => p.ApplyRules());
}

public readonly record struct Rule(Pair From, char To);

public readonly record struct Pair(char First, char Second)
{
    public static Pair FromList(IList<char> chars)
    {
        if (chars.Count != 2)
        {
            throw new ArgumentException("Requires exactly two characters.", nameof(chars));
        }

        return new Pair(chars[0], chars[1]);
    }
}