namespace Common.Parsers;

public class LineSeparatedParser<TOutput> : IParser<IList<TOutput>>
{
    private readonly IParser<TOutput> _parser;

    public LineSeparatedParser(IParser<TOutput> parser) => _parser = parser;

    public IList<TOutput> Parse(StreamReader stream)
    {
        var outputs = new List<TOutput>();
        while (!stream.EndOfStream)
        {
            var output = _parser.Parse(stream);
            outputs.Add(output);
        }

        return outputs;
    }
}
