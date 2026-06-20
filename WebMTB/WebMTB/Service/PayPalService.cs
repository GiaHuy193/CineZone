using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WebMTB.Service
{
    public class PayPalService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public PayPalService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        private string ClientId => _configuration["PayPal:ClientId"] ?? "";
        private string Secret => _configuration["PayPal:Secret"] ?? "";
        private string BaseUrl => _configuration["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";
        private string Currency => _configuration["PayPal:Currency"] ?? "USD";

        public decimal VndToUsdRate
        {
            get
            {
                var rateText = _configuration["PayPal:VndToUsdRate"];
                return decimal.TryParse(rateText, out var rate) ? rate : 26000m;
            }
        }

        public decimal ConvertVndToUsd(decimal amountVnd)
        {
            return Math.Round(amountVnd / VndToUsdRate, 2);
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var authToken = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{ClientId}:{Secret}")
            );

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/v1/oauth2/token"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" }
            });

            using var response = await _httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Không lấy được PayPal access token: " + json);
            }

            using var document = JsonDocument.Parse(json);

            return document.RootElement.GetProperty("access_token").GetString() ?? "";
        }

        public async Task<string> CreateOrderAsync(
            decimal amountUsd,
            string returnUrl,
            string cancelUrl,
            int bookingId)
        {
            var accessToken = await GetAccessTokenAsync();

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/v2/checkout/orders"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = bookingId.ToString(),
                        description = $"CineZone Booking #{bookingId}",
                        amount = new
                        {
                            currency_code = Currency,
                            value = amountUsd.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                        }
                    }
                },
                application_context = new
                {
                    return_url = returnUrl,
                    cancel_url = cancelUrl,
                    brand_name = "CineZone",
                    landing_page = "LOGIN",
                    user_action = "PAY_NOW"
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);

            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Không tạo được PayPal order: " + json);
            }

            using var document = JsonDocument.Parse(json);

            var links = document.RootElement.GetProperty("links");

            foreach (var link in links.EnumerateArray())
            {
                string rel = link.GetProperty("rel").GetString() ?? "";

                if (rel == "approve")
                {
                    return link.GetProperty("href").GetString() ?? "";
                }
            }

            throw new Exception("Không tìm thấy link approve từ PayPal.");
        }

        public async Task<string> CaptureOrderAsync(string orderId)
        {
            var accessToken = await GetAccessTokenAsync();

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/v2/checkout/orders/{orderId}/capture"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);

            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Không capture được PayPal order: " + json);
            }

            using var document = JsonDocument.Parse(json);

            string status = document.RootElement.GetProperty("status").GetString() ?? "";

            if (status != "COMPLETED")
            {
                throw new Exception("PayPal order chưa hoàn tất. Status: " + status);
            }

            var purchaseUnits = document.RootElement.GetProperty("purchase_units");
            var payments = purchaseUnits[0].GetProperty("payments");
            var captures = payments.GetProperty("captures");

            string captureId = captures[0].GetProperty("id").GetString() ?? orderId;

            return captureId;
        }
    }
}