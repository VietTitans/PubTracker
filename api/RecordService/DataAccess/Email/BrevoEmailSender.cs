using System.Net.Http.Json;

namespace RecordService.DataAccess.Email;

public class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly string _fromAddress;
    private readonly string _fromName;

    public BrevoEmailSender(HttpClient httpClient, string apiKey, string fromAddress, string fromName)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _fromAddress = fromAddress ?? throw new ArgumentNullException(nameof(fromAddress));
        _fromName = fromName ?? throw new ArgumentNullException(nameof(fromName));

        _httpClient.BaseAddress = new Uri("https://api.brevo.com/");
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey ?? throw new ArgumentNullException(nameof(apiKey)));
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string? unsubscribeUrl = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["sender"] = new { email = _fromAddress, name = _fromName },
            ["to"] = new[] { new { email = toEmail } },
            ["subject"] = subject,
            ["htmlContent"] = htmlBody
        };
        if (unsubscribeUrl != null)
        {
            payload["headers"] = new Dictionary<string, string>
            {
                ["List-Unsubscribe"] = $"<{unsubscribeUrl}>",
                ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click"
            };
        }

        var response = await _httpClient.PostAsJsonAsync("v3/smtp/email", payload);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Brevo email send failed ({(int)response.StatusCode}): {body}");
        }
    }
}
