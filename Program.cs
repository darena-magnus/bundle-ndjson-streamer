using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

var httpClient = new HttpClient(){
    Timeout = TimeSpan.FromMinutes(10)
};

var token = await GetAuthTokenAsync(httpClient);
if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("Failed to retrieve access token.");
    return;
}

var fhirServerUrl = "https://app.stg.meldrx.com/api/fhir/256d74ce-ea3f-4d9a-ba57-a6bc99662093";

using var request = new HttpRequestMessage(HttpMethod.Post, fhirServerUrl + "/$stream")
{
    Content = new ChunkedStreamContent(WriteNdjsonAsync)
};
request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
request.Headers.Add("PackageName", "cassy again");
request.Headers.Add("Source", "Magnus-Hospital");

request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-ndjson");

var cts = new CancellationTokenSource();
var sendRequest = httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

var response = await sendRequest;
Console.WriteLine(response);

static async Task<string> GetAuthTokenAsync(HttpClient client)
{
    var request = new HttpRequestMessage(HttpMethod.Post, "https://app.stg.meldrx.com/connect/token")
    {
        Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", "0aad47edc80b4b59b1f5e65224284d08"),
            new KeyValuePair<string, string>("client_secret", "OYhkVIwVx3tyXhEA2npwn0d1yIPpNG"),
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("scope", "meldrx-api cds patient/*.*")
        })
    };

    var response = await client.SendAsync(request);
    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"Token request failed: {response.StatusCode}");
        return string.Empty;
    }

    var json = await response.Content.ReadAsStringAsync();
    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);

    return tokenResponse?.AccessToken ?? string.Empty;
}

static async Task WriteNdjsonAsync(Stream stream)
{
    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "small-new-bundle.ndjson");

    if (!File.Exists(filePath))
    {
        Console.WriteLine("Error: bundle.json not found.");
        return;
    }
    
    await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new StreamReader(fileStream);

    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        await writer.WriteLineAsync(line);
        await writer.FlushAsync();

    }
}

public class ChunkedStreamContent : HttpContent
{
    private readonly Func<Stream, Task> _onStreamAvailable;

    public ChunkedStreamContent(Func<Stream, Task> onStreamAvailable)
    {
        _onStreamAvailable = onStreamAvailable;
    }

    protected async override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await _onStreamAvailable(stream);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }
}