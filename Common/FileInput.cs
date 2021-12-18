using Superpower;
using Superpower.Parsers;

namespace Common;

public sealed class FileInput
{
    private string SubPath { get; init; }

    private FileInput(string subPath) => SubPath = subPath;

    public static FileInput FromPath(string path) => new(path);

    public TOutput ParseWith<TToken, TOutput>(Tokenizer<TToken> tokenizer, TokenListParser<TToken, TOutput> parser)
    {
        var input = GetInputString();

        var tokens = tokenizer.TryTokenize(input);

        Character.WhiteSpace.IgnoreMany().AtEnd().Parse(tokens.Remainder.ToStringValue());

        if (!tokens.HasValue)
        {
            throw new ParseException(tokens.FormatErrorMessageFragment(), tokens.ErrorPosition);

        }
        
        var output = parser.AtEnd().TryParse(tokens.Value);

        if (!output.HasValue)
        {
            throw new ParseException(output.FormatErrorMessageFragment(), output.ErrorPosition);
        }

        return output.Value;
    }

    public TOutput ParseWith<TOutput>(TextParser<TOutput> parser)
    {
        var input = GetInputString();

        var output = parser.TryParse(input);

        if (!output.HasValue)
        {
            throw new ParseException(output.FormatErrorMessageFragment(), output.ErrorPosition);
        }

        Character.WhiteSpace.IgnoreMany().AtEnd().Parse(output.Remainder.ToStringValue());

        return output.Value;
    }

    private string GetInputString()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(basePath, SubPath);
        using var stream = File.OpenText(path);

        return stream.ReadToEnd();
    }
}
