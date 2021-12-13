using Common;
using System.Collections.Immutable;
using Superpower;
using Superpower.Parsers;

var nodeParser = Character.Letter.AtLeastOnce().Select(x => new Node(new string(x)));
var examples = new[] { "data/example1", "data/example2", "data/example3" };
var configuration = new Configuration
{
    PartOneExamplePaths = examples,
    PartTwoExamplePaths = examples,
};

new Runner(configuration).Run(
    nodeParser.Then(n1 => Character.EqualTo('-').IgnoreThen(nodeParser).Select(n2 => new Edge(n1, n2)))
        .Try()
        .AtLeastOnceDelimitedBy(Span.WhiteSpace)
        .Select(edges => new Graph(edges)),
    graph => graph.CountPaths(Node.Start, Node.End, false),
    graph => graph.CountPaths(Node.Start, Node.End, true)
);

public sealed class Graph
{
    private readonly ILookup<Node, Node> _neighbors;

    public Graph(IEnumerable<Edge> edges) =>
        _neighbors = edges.Concat(edges.Select(e => e.Flipped)).ToLookup(e => e.Node1, e => e.Node2);

    public int CountPaths(in Node from, in Node to, bool canVisitSomeSmallNodeTwice) =>
        CountPaths(to, from, ImmutableHashSet<Node>.Empty, canVisitSomeSmallNodeTwice);

    private int CountPaths(Node to, in Node current, ImmutableHashSet<Node> visitedSmall, bool canVisitSomeSmallNodeTwice)
    {
        if (to == current)
        {
            return 1;
        }

        var newVisited = current.IsSmall ? visitedSmall.Add(current) : visitedSmall;

        var withoutVisitingSmallTwice = _neighbors[current]
            .Where(n => !visitedSmall.Contains(n))
            .Select(n => CountPaths(to, n, newVisited, canVisitSomeSmallNodeTwice))
            .Sum();

        var withVisitingSmallTwice = 0;

        if (canVisitSomeSmallNodeTwice)
        {
            withVisitingSmallTwice = _neighbors[current]
                .Where(n => n != Node.Start && n != Node.End && visitedSmall.Contains(n))
                .Select(n => CountPaths(to, n, newVisited, false))
                .Sum();
        }

        return withoutVisitingSmallTwice + withVisitingSmallTwice;
    }
}

public readonly record struct Edge(Node Node1, Node Node2)
{
    public Edge Flipped => new Edge(Node2, Node1);
}

public readonly record struct Node
{
    public string Label { get; init; }

    public bool IsSmall => char.IsLower(Label[0]);

    public static Node Start => new Node("start");

    public static Node End => new Node("end");

    public Node(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            throw new ArgumentNullException(nameof(label));
        }

        Label = label;
    }
}