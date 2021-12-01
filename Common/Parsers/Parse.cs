namespace Common.Parsers;

public static class Parse
{
    public static IParser<int> Integer => new IntegerParser();
}
