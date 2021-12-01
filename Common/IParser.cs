namespace Common;

public interface IParser<TOutput>
{
    TOutput Parse(StreamReader stream);
}
