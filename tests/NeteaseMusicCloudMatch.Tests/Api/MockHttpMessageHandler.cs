using System.Net;
using System.Text;

namespace NeteaseMusicCloudMatch.Tests.Api;

internal sealed record CapturedRequest(HttpMethod Method, Uri? Uri, string? Body, string? ContentType);

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses;

    public MockHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
    }

    public List<CapturedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri, body, request.Content?.Headers.ContentType?.MediaType));
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No mock response configured.");
        }

        return _responses.Dequeue()(request);
    }

    public static Func<HttpRequestMessage, HttpResponseMessage> Json(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
