using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Choir;
using Choir.Diagnostics;
using Choir.Source;

namespace Canal;

public sealed class CanalContext(DiagnosticEngine diag, CanalOptions options, SourceText checkSource)
{
    public const string PrefixDirectiveName = "FCHK-PREFIX";
    public const string RunWithPrefixDirectiveNameStart = "R[";

    private static readonly Dictionary<StringView, CanalDirective> _directiveMap = new()
    {
        { "*", CanalDirective.CheckAny },
        { "+", CanalDirective.CheckNext },
        { "!", CanalDirective.CheckNotSame },
        { "!*", CanalDirective.CheckNotAny },
        { "!+", CanalDirective.CheckNotNext },
        { "re*", CanalDirective.RegexCheckAny },
        { "re+", CanalDirective.RegexCheckNext },
        { "re!", CanalDirective.RegexCheckNotSame },
        { "re!*", CanalDirective.RegexCheckNotAny },
        { "re!+", CanalDirective.RegexCheckNotNext },
        { "b", CanalDirective.Begin },
        { "d", CanalDirective.Define },
        { "e", CanalDirective.ExpandDefine },
        { "u", CanalDirective.Undefine },
        { "p", CanalDirective.Pragma },
        { PrefixDirectiveName, CanalDirective.Prefix },
        { "R", CanalDirective.Run },
        { "V", CanalDirective.Verify },
        { "X", CanalDirective.ExpectFail },
    };

    private static readonly Dictionary<CanalDirective, string> _nameMap = new()
    {
        { CanalDirective.CheckAny, "*" },
        { CanalDirective.CheckNext, "+" },
        { CanalDirective.CheckNotSame, "!" },
        { CanalDirective.CheckNotAny, "!*" },
        { CanalDirective.CheckNotNext, "!+" },
        { CanalDirective.RegexCheckAny, "re*" },
        { CanalDirective.RegexCheckNext, "re+" },
        { CanalDirective.RegexCheckNotSame, "re!" },
        { CanalDirective.RegexCheckNotAny, "re!*" },
        { CanalDirective.RegexCheckNotNext, "re!+" },
        { CanalDirective.Begin, "b" },
        { CanalDirective.Define, "d" },
        { CanalDirective.ExpandDefine, "e" },
        { CanalDirective.Undefine, "u" },
        { CanalDirective.Pragma, "p" },
        { CanalDirective.Prefix, PrefixDirectiveName },
        { CanalDirective.Run, "R" },
        { CanalDirective.Verify, "V" },
        { CanalDirective.ExpectFail, "X" },
    };

    public DiagnosticEngine Diag { get; } = diag;
    public CanalOptions Options { get; } = options;
    public SourceText CheckSource { get; } = checkSource;

    private readonly Dictionary<string, CanalPrefixState> _prefixStates = [];
    private readonly List<CanalTest> _runDirectives = [];

    private void VerboseLog(string message)
    {
        if (!Options.VerboseOutput) return;
        Console.Error.WriteLine(message);
    }

    public CanalPrefixState CreatePrefix(string prefix)
    {
        var state = new CanalPrefixState()
        {
            Prefix = prefix,
        };

        // default state from the options
        state.LiteralCharacters.AddRange(Options.LiteralCharacters);
        state.ForceRegex = Options.ForceRegex;
        state.NoCapture = Options.NoCapture;
        state.CaptureTypes = Options.CaptureTypes;

        _prefixStates[prefix] = state;
        return state;
    }

    public void CollectDirectives(CanalPrefixState state)
    {
        var lineInfos = CheckSource.GetLineInfos();

        foreach (var lineInfo in lineInfos)
        {
            // "parse" the line, and only continue if we hit what might be a valid directive
            var lineText = lineInfo.LineText.TrimStart();
            if (!lineText.StartsWith(state.Prefix)) continue;
            lineText = lineText[state.Prefix.Length..];
            if (lineText.Length == 0 || !char.IsWhiteSpace(lineText[0])) continue;
            lineText = lineText.TrimStart();
            if (lineText.Length == 0) continue;

            // but first, check for the special-case run directive with a separate prefix
            if (lineText.StartsWith(RunWithPrefixDirectiveNameStart))
            {
                string newPrefix = (string)lineText[RunWithPrefixDirectiveNameStart.Length..].TakeUntil(']', '\r', '\n');
                lineText = lineText[newPrefix.Length..];

                if (!lineText.StartsWith(']')) continue;
                lineText = lineText[1..];

                if (!_prefixStates.TryGetValue(newPrefix, out var newState))
                {
                    _prefixStates[newPrefix] = newState = CreatePrefix(newPrefix);
                    CollectDirectives(newState);
                }

                _runDirectives.Add(new CanalTest()
                {
                    RunDirective = (string)lineText.Trim(),
                    State = newState,
                });

                continue;
            }

            var directiveName = lineText.TakeUntil(char.IsWhiteSpace);
            lineText = lineText[directiveName.Length..];

            // now, is this a prefix?
            if (!_directiveMap.TryGetValue(directiveName, out var directive))
                continue;

            var value = lineText.TrimStart();

            // any additional prefix directives are bad, ignore them.
            if (directive == CanalDirective.Prefix)
            {
                if (state.Prefix != value)
                {
                    Diag.Emit(DiagnosticLevel.Warning, CheckSource, lineInfo.LineStart + lineInfo.LineText.IndexOf(PrefixDirectiveName), $"Conflicting prefix directive '{value}' ignored.");
                    Diag.Emit(DiagnosticLevel.Note, $"Current prefix is '{state.Prefix}'.");
                }

                continue;
            }

            // handle the test directives.
            if (directive is CanalDirective.Run or CanalDirective.Verify or CanalDirective.ExpectFail)
            {
                _runDirectives.Add(new CanalTest()
                {
                    RunDirective = (string)value,
                    State = state,
                    VerifyOnly = directive is not CanalDirective.Run,
                    ExpectFailure = directive is CanalDirective.ExpectFail,
                });

                continue;
            }

            // TODO(local): this will break if the prefix contains the directive name, but it's close enough for now.
            SourceLocation location = lineInfo.LineStart + lineInfo.LineText.IndexOf(directiveName);
            switch (directive)
            {
                case CanalDirective.Run:
                case CanalDirective.Verify:
                case CanalDirective.ExpectFail:
                case CanalDirective.Prefix:
                {
                    Diag.Emit(DiagnosticLevel.Fatal, $"Should have already handled directive {directive}.");
                    throw new UnreachableException();
                }

                case CanalDirective.CheckAny:
                case CanalDirective.CheckNext:
                case CanalDirective.CheckNotSame:
                case CanalDirective.CheckNotAny:
                case CanalDirective.CheckNotNext:
                {
                    if (state.ForceRegex)
                    {
                        directive += (int)CanalDirective.RegexCheckAny;
                        goto case CanalDirective.RegexCheckAny;
                    }

                    state.Checks.Add(new CanalCheck()
                    {
                        Directive = directive,
                        Location = location,
                        StringData = value.ToString().FoldWhiteSpace(),
                    });
                } break;

                case CanalDirective.RegexCheckAny:
                case CanalDirective.RegexCheckNext:
                case CanalDirective.RegexCheckNotSame:
                case CanalDirective.RegexCheckNotAny:
                case CanalDirective.RegexCheckNotNext:
                {
                    try
                    {
                        string expr = SubstituteNoCapture(value.ToString().FoldWhiteSpace());
                        // TODO(local): construct an environment regex if captures are used

                        state.Checks.Add(new CanalCheck()
                        {
                            Directive = directive,
                            Location = location,
                            RegexData = new Regex(expr),
                        });
                    }
                    catch (RegexParseException regexException)
                    {
                        Diag.Emit(DiagnosticLevel.Error, CheckSource, location, $"Invalid regular expression: {regexException.Message}");
                    }
                } break;
            }
        }

        string SubstituteNoCapture(string expr)
        {
            if (state.NoCapture)
            {
                int i = 0;
                Escape(true);

                void Escape(bool topLevel = false)
                {
                    while (i < expr.Length)
                    {
                        i = expr.IndexOfAny(['(', ')']);
                        if (i < 0) return;

                        // At a closing paren, let ur caller handle this
                        // if we’re not at the top level; if we are, just
                        // escape it.
                        if (expr[i] == ')')
                        {
                            if (!topLevel) return; ;
                            expr = expr[..i] + "\\" + expr[i..];
                            i += 2;
                            continue;
                        }

                        // Escape any opening parens at the end.
                        if (i == expr.Length - 1)
                        {
                            expr = expr[..i] + "\\" + expr[i..];
                            i += 2;
                            return;
                        }

                        // If the next character is a '?', recurse to handle
                        // nested parens, and leave the matching closing paren
                        // as is.
                        if (expr[i + 1] == '?')
                        {
                            i++;
                            Escape();
                            if (i < expr.Length && expr[i] == ')')
                                i++;
                            continue;
                        }

                        // Otherwise, escape the paren, recurse to take care of
                        // nested parens, and escape the corresponding closing
                        // paren, if there is one.
                        expr = expr[..i] + "\\" + expr[i..];
                        i += 2;
                        Escape();

                        if (i < expr.Length && expr[i] == ')')
                        {
                            expr = expr[..i] + "\\" + expr[i..];
                            i += 2;
                        }
                    }
                }
            }

            foreach (char c in state.LiteralCharacters)
                expr = expr.Replace($"{c}", $"\\{c}");

            return expr;
        }
    }

    public void Process()
    {
        if (_runDirectives.Count == 0)
        {
            Diag.Emit(DiagnosticLevel.Error, $"No run ('{_nameMap[CanalDirective.Run]}', '{_nameMap[CanalDirective.Verify]}' or '{_nameMap[CanalDirective.ExpectFail]}') directives found in check file.");
            return;
        }

        foreach (var test in _runDirectives)
        {
            RunTest(test);
            if (Diag.HasEmittedErrors && Options.AbortOnFirstFailedCheck)
                break;
        }
    }

    public void RunTest(CanalTest test)
    {
        string command = test.RunDirective;
        string checkFilePath = Options.CheckFile!.FullName;

        // TODO(local): this should probably do more sophisticated replacements maybe? because spaces.
        foreach (var (key, value) in Options.DefinedConstants)
            command = command.Replace(key, value);
        command = command.Replace("%s", checkFilePath);

        if (command.Contains('%'))
        {
            int percentIndex = command.IndexOf('%');
            var def = ((StringView)command)[percentIndex..].TakeUntil(char.IsWhiteSpace);

            var runSource = new SourceText("<run>", test.RunDirective, SourceLanguage.None);
            SourceLocation defLocation = test.RunDirective.IndexOf((string)def);

            Diag.Emit(DiagnosticLevel.Error, runSource, defLocation, $"Variable '{def}' is not defined.");
            Diag.Emit(DiagnosticLevel.Note, $"Define it on the command-line using '-D {def}=<value>'.");

            // no reason to run this test, it's broken already.
            return;
        }

        var commandSource = new SourceText("<command>", command, SourceLanguage.None);
        SourceLocation checkFileLocation = command.IndexOf(checkFilePath);

        var result = RunCommand(command);

        if (!result.Success)
        {
            // expected to fail, so this is totally fine.
            if (test.ExpectFailure)
                return;

            if (test.VerifyOnly)
                Console.Error.WriteLine(result.Output);
            else Diag.Emit(DiagnosticLevel.Error, commandSource, checkFileLocation, $"Command '{command}' failed: {result.ErrorMessage}");

            return;
        }

        // we didn't fail, so this is itself a failure.
        if (test.ExpectFailure)
        {
            Diag.Emit(DiagnosticLevel.Error, commandSource, checkFileLocation, $"Command '{command}' succeeded, but was supposed to fail.");
            return;
        }

        // if we're just verifying, then we're done.
        if (test.VerifyOnly)
            return;

        var inputSource = new SourceText("<input>", result.Output, SourceLanguage.None);
        CanalMatcher.Match(this, inputSource, test.State.Checks);
    }

    private CanalTestResult RunCommand(string commandText)
    {
        VerboseLog($"[CANAL] Running command: '{commandText}'.");

        StringView processName = ((StringView)commandText).TakeUntil(char.IsWhiteSpace);
        StringView arguments = ((StringView)commandText)[processName.Length..].TrimStart();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && processName == "cat")
        {
            try
            {
                return new CanalTestResult()
                {
                    Success = true,
                    Output = File.ReadAllText((string)arguments),
                };
            }
            catch (Exception ex)
            {
                return new CanalTestResult()
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                };
            }
        }

        var startInfo = new ProcessStartInfo((string)processName, (string)arguments)
        {
            RedirectStandardOutput = true,
        };

        try
        {
            var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                return new CanalTestResult()
                {
                    Success = false,
                    ErrorMessage = "Process failed to start.",
                };
            }

            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            return new CanalTestResult()
            {
                Success = process.ExitCode == 0,
                Output = output,
                ErrorMessage = process.ExitCode == 0 ? "" : $"Process exited with status {process.ExitCode}.",
            };
        }
        catch (Exception ex)
        {
            return new CanalTestResult()
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }
}
