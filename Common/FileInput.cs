using Superpower;
using Superpower.Parsers;

namespace Common;

public sealed class FileInput
{
    private string SubPath { get; init; }

    private FileInput(string subPath) => SubPath = subPath;

    public static FileInput FromPath(string path) => new(path);

    public TOutput ParseAs<TOutput>(TextParser<TOutput> parser)
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(basePath, SubPath);
        using var stream = File.OpenText(path);
        var input = stream.ReadToEnd();

        var output = parser.TryParse(input);

        if (!output.HasValue)
        {
            throw new ParseException(output.FormatErrorMessageFragment(), output.ErrorPosition);
        }

        Character.WhiteSpace.IgnoreMany().AtEnd().Parse(output.Remainder.ToStringValue());

        return output.Value;
    }
}
