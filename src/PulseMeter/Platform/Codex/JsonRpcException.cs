namespace PulseMeter.Platform.Codex;

public sealed class JsonRpcException : Exception
{
    public JsonRpcException(int? code, string message)
        : base(message)
    {
        Code = code;
    }

    public int? Code { get; }
}
