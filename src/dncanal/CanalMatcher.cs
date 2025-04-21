
using System.Diagnostics;
using System.Text.RegularExpressions;

using Choir;
using Choir.Diagnostics;
using Choir.Source;

namespace Canal;

public sealed class CanalMatcher
{
    public static void Match(CanalContext context, SourceText source, IReadOnlyList<CanalCheck> checks)
    {
        var matcher = new CanalMatcher(context, source, checks);
        matcher.Match();
    }

    private readonly CanalContext _context;
    private readonly SourceText _source;
    private readonly IReadOnlyList<CanalCheck> _checks;
    private int _cursor;

    private readonly IReadOnlyList<SourceLocationInfo> _lineInfos;
    private int _line, _prevLine;

    private CanalCheck Check => _checks[_cursor];
    private StringView LineText => _lineInfos[_line].LineText;

    private CanalMatcher(CanalContext context, SourceText source, IReadOnlyList<CanalCheck> checks)
    {
        _context = context;
        _source = source;
        _checks = checks;

        _lineInfos = source.GetLineInfos();
    }

    private void Match()
    {
        for (; _cursor < _checks.Count && _line < _lineInfos.Count; _cursor++)
        {
            try
            {
                Step();
            }
            catch (RegexParseException regexException)
            {
                _context.Diag.Emit(DiagnosticLevel.Error, _context.CheckSource, Check.Location, $"Invalid regular expression: {regexException.Message}");
            }
        }

        while (_cursor < _checks.Count && Check.Directive is CanalDirective.CheckNotAny or CanalDirective.CheckNotNext or CanalDirective.CheckNotSame or CanalDirective.RegexCheckNotAny or CanalDirective.RegexCheckNotNext or CanalDirective.RegexCheckNotSame)
            _cursor++;

        if (_cursor < _checks.Count && !_context.Diag.HasEmittedErrors)
            _context.Diag.Emit(Choir.Diagnostics.DiagnosticLevel.Error, "End of file reached looking for string.");
    }
    
    private void NextLine()
    {
        _prevLine = _line;
        _line++;
    }

    private void SkipCheckNextDirectives()
    {
        while (_cursor < _checks.Count - 1)
        {
            if (_checks[_cursor + 1].Directive is CanalDirective.CheckNext or CanalDirective.CheckNotNext or CanalDirective.RegexCheckNext or CanalDirective.RegexCheckNotNext)
                _cursor++;
            else return;
        }
    }

    private bool CheckLineMatches()
    {
        if (Check.StringData is { } stringData)
            return LineText.Contains(stringData);
        else if (Check.RegexData is { } regexData)
            return regexData.IsMatch(LineText);

        Debug.Assert(Check.EnvironmentRegexData is not null);
        return EnvironmentRegexIsMatch(Check.EnvironmentRegexData);
    }

    private bool EnvironmentRegexIsMatch(EnvironmentRegex env)
    {
        throw new NotImplementedException();
    }

    private void Step()
    {
        throw new NotImplementedException();
    }
}
