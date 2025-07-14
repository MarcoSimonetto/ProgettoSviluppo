using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;

namespace ProxyManager.Controllers;

[ApiController]
[Route("{**path}")]
public class ProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public ProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet, HttpPost, HttpPut, HttpDelete]
    public async Task<IActionResult> ForwardRequest(string path)
    {
        var backendUrl = $"http://localhost:5099/{path}";

        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(Request.Method),
            RequestUri = new Uri(backendUrl)
        };

        if (Request.ContentLength > 0)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        foreach (var header in Request.Headers)
        {
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.Authorization =
                    System.Net.Http.Headers.AuthenticationHeaderValue.Parse(header.Value);
            }
            else
            {
                // Try to add to main headers, else to content headers
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                {
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
        }

        var response = await _httpClient.SendAsync(requestMessage);
        var content = await response.Content.ReadAsStringAsync();

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = content,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
        };
    }
}