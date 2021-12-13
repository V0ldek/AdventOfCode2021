namespace Common;

public sealed class Configuration
{
    public string PartOneInputPath { get; init; } = "data/input";

    public IReadOnlyList<string> PartOneExamplePaths { get; init; } = new[] { "data/example" };

    public string PartTwoInputPath { get; init; } = "data/input";

    public IReadOnlyList<string> PartTwoExamplePaths { get; init; }

    public Configuration() => PartTwoExamplePaths = PartOneExamplePaths;
}
