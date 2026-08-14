using System.Net;
using System.Text;

namespace Quotes.Tests.Unit.TestSupport;

// Registered via ConfigurePrimaryHttpMessageHandler, so despite the DelegatingHandler
// base type this is the terminal handler in the chain - it never calls base.SendAsync,
// it just hands back the next queued status code. That's what lets the resilience
// handler sitting in front of it genuinely retry: each retry is a real re-invocation
// of SendAsync, not a canned single response.
public class StubHttpMessageHandler : DelegatingHandler
{
    private readonly Queue<HttpStatusCode> _responses;

    public StubHttpMessageHandler(params HttpStatusCode[] responses)
    {
        _responses = new Queue<HttpStatusCode>(responses);
    }

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;

        if (_responses.Count == 0)
            throw new InvalidOperationException("StubHttpMessageHandler received more calls than responses were queued for.");

        var status = _responses.Dequeue();
        var response = new HttpResponseMessage(status);

        if (status == HttpStatusCode.OK)
        {
            response.Content = new StringContent(
                """[{"q":"Test quote.","a":"Test Author"}]""",
                Encoding.UTF8,
                "application/json");
        }

        return Task.FromResult(response);
    }
}
