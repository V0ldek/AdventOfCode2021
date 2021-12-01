namespace Common;

public sealed class FileInput
{
    private string SubPath { get; init; }

    private FileInput(string subPath) => SubPath = subPath;

    public static FileInput FromPath(string path) => new(path);

    public TOutput ParseAs<TOutput>(IParser<TOutput> parser)
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(basePath, SubPath);
        using var stream = File.OpenText(path);

        return parser.Parse(stream);
    }
}
