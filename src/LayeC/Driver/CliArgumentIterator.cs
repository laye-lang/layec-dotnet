namespace LayeC.Driver;

public sealed class CliArgumentIterator(string[] args, int startIndex = 0)
{
    private int _index = startIndex;

    public int RemainingCount => args.Length - _index;

    public bool Shift(out string arg)
    {
        arg = "";

        if (_index >= args.Length) return false;

        arg = args[_index++];
        return true;
    }

    public string? Peek()
    {
        if (_index >= args.Length)
            return null;
        else return args[_index];
    }
}
