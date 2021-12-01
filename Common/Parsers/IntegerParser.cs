namespace Common.Parsers;

public sealed class IntegerParser : IParser<int>
{
    public int Parse(StreamReader stream)
    {
        var line = stream.ReadLine();

        if (line is null)
        {
            throw new InvalidOperationException("Expected integer, found EoF.");
        }

        return int.Parse(line);
    }
}
