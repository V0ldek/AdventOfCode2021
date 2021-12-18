using Superpower;

namespace Common;

public sealed class Runner
{
    private readonly Configuration _configuration;

    public Runner() => _configuration = new();

    public Runner(Configuration configuration) => _configuration = configuration;

    public void Run<TOutput>(TextParser<TOutput> parser, Func<TOutput, object> solutionOne) =>
        Run(parser, solutionOne, null);

    public void Run<TOutput>(
        TextParser<TOutput> parser,
        Func<TOutput, object> solutionOne,
        Func<TOutput, object>? solutionTwo) =>
        Run(i => i.ParseWith(parser), solutionOne, solutionTwo);

    public void Run<TToken, TOutput>(
        Tokenizer<TToken> tokenizer,
        TokenListParser<TToken, TOutput> parser,
        Func<TOutput, object> solutionOne) =>
        Run(tokenizer, parser, solutionOne, null);

    public void Run<TToken, TOutput>(
        Tokenizer<TToken> tokenizer,
        TokenListParser<TToken, TOutput> parser,
        Func<TOutput, object> solutionOne,
        Func<TOutput, object>? solutionTwo) =>
        Run(i => i.ParseWith(tokenizer, parser), solutionOne, solutionTwo);

    private void Run<TOutput>(
        Func<FileInput, TOutput> parse,
        Func<TOutput, object> solutionOne,
        Func<TOutput, object>? solutionTwo)
    {
        var part = solutionTwo is null ? 1 : ReadPart();
        var decision = ReadDecision();

        Console.WriteLine($"Running part {part} on {decision.ToString()!.ToLower()}...");

        var (inputPaths, solution) = (part, decision) switch
        {
            (1, Decision.Example) => (_configuration.PartOneExamplePaths, solutionOne),
            (1, Decision.Input) => (new[] { _configuration.PartOneInputPath }, solutionOne),
            (2, Decision.Example) => (_configuration.PartTwoExamplePaths, solutionTwo),
            (2, Decision.Input) => (new[] { _configuration.PartTwoInputPath }, solutionTwo),
            _ => throw new InvalidOperationException()
        };

        try
        {
            if (inputPaths.Count == 1)
            {
                var input = FileInput.FromPath(inputPaths[0]);
                var parsed = parse(input);
                var result = solution!(parsed);

                Console.WriteLine(result);
            }
            else
            {
                for (var i = 0; i < inputPaths.Count; i += 1)
                {
                    var input = FileInput.FromPath(inputPaths[i]);
                    var parsed = parse(input);
                    var result = solution!(parsed);

                    Console.WriteLine($"{decision} {i + 1}:");
                    Console.WriteLine(result);
                }
            }
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
