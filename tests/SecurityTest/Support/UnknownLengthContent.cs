using System.Text;

namespace Knock.SecurityTest.Support;

/// <summary>Cuerpo sin <c>Content-Length</c> (transferencia por trozos): el servidor debe cortarlo por su cuenta al pasar el límite.</summary>
internal sealed class UnknownLengthContent : HttpContent
{
    private readonly byte[] _bytes;

    public UnknownLengthContent(string text)
    {
        _bytes = Encoding.UTF8.GetBytes(text);
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
    }

    protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(_bytes).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}
