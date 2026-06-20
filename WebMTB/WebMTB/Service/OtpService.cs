using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace WebMTB.Service
{
    public class OtpService
    {
        private readonly IMemoryCache _cache;

        public OtpService(IMemoryCache cache)
        {
            _cache = cache;
        }

        private string NormalizeEmail(string email)
        {
            return email.Trim().ToLower();
        }

        private string BuildOtpKey(string purpose, string email)
        {
            return $"otp:{purpose}:{NormalizeEmail(email)}";
        }

        private string BuildCooldownKey(string purpose, string email)
        {
            return $"otp-cooldown:{purpose}:{NormalizeEmail(email)}";
        }

        public (bool Success, string Code, string Message) GenerateOtp(
            string email,
            string purpose,
            int expireMinutes = 5,
            int cooldownSeconds = 90)
        {
            email = NormalizeEmail(email);

            string cooldownKey = BuildCooldownKey(purpose, email);

            if (_cache.TryGetValue(cooldownKey, out _))
            {
                return (false, "", "Bạn vừa yêu cầu gửi mã OTP. Vui lòng chờ một lúc trước khi gửi lại.");
            }

            int number = RandomNumberGenerator.GetInt32(100000, 999999);
            string code = number.ToString();

            var data = new OtpData
            {
                Code = code,
                AttemptCount = 0,
                ExpiredAt = DateTime.Now.AddMinutes(expireMinutes)
            };

            _cache.Set(BuildOtpKey(purpose, email), data, TimeSpan.FromMinutes(expireMinutes));
            _cache.Set(cooldownKey, true, TimeSpan.FromSeconds(cooldownSeconds));

            return (true, code, "Tạo mã OTP thành công.");
        }

        public (bool Success, string Message) ValidateOtp(
            string email,
            string purpose,
            string code,
            int maxAttempts = 5)
        {
            email = NormalizeEmail(email);
            code = code?.Trim() ?? "";

            string otpKey = BuildOtpKey(purpose, email);

            if (!_cache.TryGetValue(otpKey, out OtpData data))
            {
                return (false, "Mã OTP không tồn tại hoặc đã hết hạn.");
            }

            if (DateTime.Now > data.ExpiredAt)
            {
                _cache.Remove(otpKey);
                return (false, "Mã OTP đã hết hạn. Vui lòng yêu cầu gửi lại mã.");
            }

            if (data.AttemptCount >= maxAttempts)
            {
                _cache.Remove(otpKey);
                return (false, "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu gửi lại mã OTP.");
            }

            if (data.Code != code)
            {
                data.AttemptCount++;
                _cache.Set(otpKey, data, data.ExpiredAt - DateTime.Now);

                int remain = maxAttempts - data.AttemptCount;
                return (false, $"Mã OTP không đúng. Bạn còn {remain} lần thử.");
            }

            _cache.Remove(otpKey);
            return (true, "Xác thực OTP thành công.");
        }

        private class OtpData
        {
            public string Code { get; set; } = "";
            public int AttemptCount { get; set; }
            public DateTime ExpiredAt { get; set; }
        }
    }
}