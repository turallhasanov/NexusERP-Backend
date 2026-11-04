using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace NexusERP.Api.IntegrationTests.TestSupport;

public static class HttpExtensions
{
    /// <summary>The body as raw JSON, so tests check the wire names (camelCase) the frontends and Khazar read.</summary>
    public static async Task<JsonObject> JsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(text)?.AsObject() ?? throw new InvalidOperationException($"Body is not a JSON object: {text}");
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body);

    /// <summary>{ "ok": false, "error": message }, the ErrorResponse every non-exception failure uses.</summary>
    public static async Task ShouldBeErrorAsync(this HttpResponseMessage response, System.Net.HttpStatusCode status, string message)
    {
        var body = await response.JsonAsync();
        response.StatusCode.Should().Be(status, body.ToJsonString());
        body["ok"]!.GetValue<bool>().Should().BeFalse();
        body["error"]!.GetValue<string>().Should().Be(message);
    }
}
