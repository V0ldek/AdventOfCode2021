namespace Common.Parsers;

public static class IParserExtensions
{
    public static IParser<IList<TOutput>> LineSeparated<TOutput>(this IParser<TOutput> parser) =>
        new LineSeparatedParser<TOutput>(parser);
}
