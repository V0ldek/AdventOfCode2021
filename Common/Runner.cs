using Superpower;

namespace Common;

public sealed class Runner
{
    private readonly Configuration _configuration;

    public Runner() => _configuration = new();

    public Runner(Configuration configuration) => _configuration = configuration;

    public void Run<TOutput, TResult>(TextParser<TOutput> parser, Func<TOutput, TResult> solutionOne) => 
        Run(parser, solutionOne, null);

    public void Run<TOutput, TResult>(
        TextParser<TOutput> parser,
        Func<TOutput, TResult> solutionOne,
        Func<TOutput, TResult>? solutionTwo)
    {
        var part = solutionTwo is null ? 1 : ReadPart();
        var decision = ReadDecision();

        Console.WriteLine($"Running part {part} on {decision.ToString()!.ToLower()}...");

        var (inputPath, solution) = (part, decision) switch
        {
            (1, Decision.Example) => (_configuration.PartOneExamplePath, solutionOne),
            (1, Decision.Input) => (_configuration.PartOneInputPath, solutionOne),
            (2, Decision.Example) => (_configuration.PartTwoExamplePath, solutionTwo),
            (2, Decision.Input) => (_configuration.PartTwoInputPath, solutionTwo),
            _ => throw new InvalidOperationException()
        };

        try
        {
            var input = FileInput.FromPath(inputPath);
            var parsed = input.ParseAs(parser);
            var result = solution!(parsed);

            Console.WriteLine(result);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed with an exception: '{e.Message}'.");
            throw;
        }
    }

    private static int ReadPart()
    {
        Console.WriteLine("Part [1] or [2]?");
        int? part = null;

        while (part is null)
        {
            var key = Console.ReadKey();

            part = key.Key switch
            {
                ConsoleKey.D1 => 1,
                ConsoleKey.D2 => 2,
                _ => null
            };

            Console.CursorLeft = 0;
            Console.Write(' ');
            Console.CursorLeft = 0;
        }

        return part.Value;
    }

    private static Decision ReadDecision()
    {
        Console.WriteLine("[E]xample or real [i]nput?");
        Decision? decision = null;

        while (decision is null)
        {
            var key = Console.ReadKey();

            decision = key.Key switch
            {
                ConsoleKey.E => Decision.Example,
                ConsoleKey.I => Decision.Input,
                _ => null
            };

            Console.CursorLeft = 0;
            Console.Write(' ');
            Console.CursorLeft = 0;
        }

        return decision.Value;
    }

    private enum Decision
    {
        Example,
        Input
    }
}
