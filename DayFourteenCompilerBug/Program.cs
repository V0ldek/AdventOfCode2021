var s = new S(new object());

Console.WriteLine(s);

public record struct S(char First, char Second)
{
    public S(object o) : this()
    {
    }
}