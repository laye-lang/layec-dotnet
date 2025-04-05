using LayeC.LanguageServer.Json;

namespace LayeC.LanguageServer;

public sealed class LanguageServerRequestMessage
{
    public string? JsonRpc { get; set; }
    public NumberOrString? Id { get; set; }
    public string? Method { get; set; }
}

public sealed class LanguageServerRequestMessageCall
{
    public string? Method { get; set; }
}
