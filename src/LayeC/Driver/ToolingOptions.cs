using LayeC.Diagnostics;
using LayeC.Source;

namespace LayeC.Driver;

public static class ToolingOptions
{
    private readonly struct LineInfo(string rawLineText, string key = "", string value = "")
    {
        public readonly string RawLineText = rawLineText;
        public readonly string Key = key;
        public readonly string Value = value;
    }

    private const string OutputColoringKey = "output-coloring";
    private const string SimpleDiagnosticsKey = "simple-diagnostics";

    public static readonly IReadOnlyDictionary<string, (string TypeString, string DefaultValue)> Keys = new Dictionary<string, (string TypeString, string DefaultValue)>()
    {
        {OutputColoringKey, ("trilean", "default")},
        {SimpleDiagnosticsKey, ("trilean", "default")},
    };

    private static readonly FileInfo _fileInfo;
    private static readonly List<LineInfo> _fileLines = [];
    private static readonly Dictionary<int, object> _cachedValues = [];

    static ToolingOptions()
    {
        DirectoryInfo configRootDir;
        if (Environment.GetEnvironmentVariable("LAYE_CONFIG_ROOT") is string layeConfigRootPath)
            configRootDir = new DirectoryInfo(layeConfigRootPath);
        else configRootDir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).ChildDirectory(".laye");

        _fileInfo = configRootDir.ChildFile("tooling.conf");
        if (!_fileInfo.Exists) return;

        using var diag = new DiagnosticEngine(new FormattedDiagnosticWriter(Console.Out, true));
        TryLoadFromFile(diag);

        static void TryLoadFromFile(DiagnosticEngine diag)
        {
            if (!_fileInfo.Exists) return;

            var sourceText = new SourceText(_fileInfo.FullName, _fileInfo.ReadAllText());
            var lineInfos = sourceText.GetLineInfos();

            for (int i = 0; i < lineInfos.Length; i++)
            {
                var lineInfo = lineInfos[i];

                var line = lineInfo.LineText.TrimStart();
                if (line.StartsWith('#') || line.IsWhiteSpace())
                {
                    _fileLines.Add(new((string)line));
                    continue;
                }

                int equalIndex = line.IndexOf('=');
                if (equalIndex < 0)
                {
                    _fileLines.Add(new((string)line));
                    diag.Emit(DiagnosticLevel.Warning, sourceText, lineInfo.LineStart, "Config line does not contain an '='. Skipping.");
                    continue;
                }

                string key = (string)line[0..equalIndex].TrimEnd();
                if (key.IsNullOrWhiteSpace())
                {
                    _fileLines.Add(new((string)line));
                    diag.Emit(DiagnosticLevel.Warning, sourceText, lineInfo.LineStart, "Config does not have a key before the '='. Skipping.");
                    continue;
                }

                string value = (string)line[(equalIndex + 1)..].Trim();
                _fileLines.Add(new((string)line, key, value));
            }
        }
    }

    public static Trilean OutputColoring
    {
        get => GetTriState(OutputColoringKey, Trilean.Unknown);
        set => SetTriState(OutputColoringKey, value);
    }

    public static Trilean SimpleDiagnostics
    {
        get => GetTriState(SimpleDiagnosticsKey, Trilean.Unknown);
        set => SetTriState(SimpleDiagnosticsKey, value);
    }

    public static string? TryGetRawKeyValue(string key, out bool isPresent)
    {
        isPresent = false;

        var li = _fileLines.Find(li => li.Key == key);
        // if this didn't find the value, Key will not match
        if (li.Key == key)
        {
            isPresent = true;
            return li.Value;
        }
        else if (Keys.TryGetValue(key, out var pair))
            return pair.DefaultValue;
        else return null;
    }

    private static int GetKeyIndex(string key) => _fileLines.FindIndex(li => li.Key == key);

    private static T GetValue<T>(string key, T defaultValue, Func<string, T> parser)
        where T : notnull
    {
        int index = GetKeyIndex(key);
        if (index < 0) return defaultValue;
        if (!_cachedValues.TryGetValue(index, out object? value))
            return (T)(_cachedValues[index] = parser(_fileLines[index].Value));
        else return (T)value;
    }

    private static void SetValue<T>(string key, T value, Func<T, string> toString)
        where T : notnull
    {
        int index = GetKeyIndex(key);

        string valueString = toString(value);
        var lineInfo = new LineInfo($"{key} = {valueString}", key, valueString);

        if (index >= 0)
            _fileLines[index] = lineInfo;
        else
        {
            index = _fileLines.Count;
            _fileLines.Add(lineInfo);
        }

        _cachedValues[index] = value;
    }

    private static bool GetBool(string key, bool defaultValue) => GetValue(key, defaultValue, value => value.Equals("true", StringComparison.CurrentCultureIgnoreCase));
    private static void SetBool(string key, bool value) => SetValue(key, value, value => value ? "true" : "false");

    private static Trilean GetTriState(string key, Trilean defaultValue) => GetValue(key, defaultValue, ParseTriState);
    private static void SetTriState(string key, Trilean value) => SetValue(key, value, value => value == Trilean.Unknown ? "default" : value == Trilean.True ? "true" : "false");

    private static Trilean ParseTriState(string value) => value.ToLower() switch
    {
        "true" or "on" => Trilean.True,
        "false" or "off" => Trilean.False,
        _ => Trilean.Unknown,
    };
}
