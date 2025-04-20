using System.Text;

using Choir;

namespace System;

public static class CanalStringExtensions
{
    public static string FoldWhiteSpace(this string s)
    {
        var builder = new StringBuilder();

        StringView sv = s;
        while (sv.Length > 0)
        {
            // take all non white-space characters, if any
            var taken = sv.TakeUntil(char.IsWhiteSpace);
            sv = sv[taken.Length..];

            // then append them to the output
            builder.Append(taken.Span);

            // take all white-space characters, if any
            taken = sv.TakeWhile(char.IsWhiteSpace);
            sv = sv[taken.Length..];

            // and replace them with a single space
            if (taken.Length != 0)
                builder.Append(' ');
        }

        return builder.ToString();
    }
}
