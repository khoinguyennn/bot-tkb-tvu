using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace BOT_TKB_TVU
{
    public class TvuApiService
    {
        private static readonly HttpClient client = new HttpClient();

        public TvuApiService()
        {
            // Cấu hình HttpClient
            client.BaseAddress = new Uri("https://ttsv.tvu.edu.vn");
            client.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            client.DefaultRequestHeaders.Add("Origin", "https://ttsv.tvu.edu.vn");
            client.DefaultRequestHeaders.Add("Referer", "https://ttsv.tvu.edu.vn/");
        }

        // Phương thức để lấy User-Agent ngẫu nhiên
        private string GetRandomUserAgent()
        {
            var userAgents = new string[]
            {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.5359.72 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.5249.119 Safari/537.36"
            };
            Random random = new Random();
            return userAgents[random.Next(userAgents.Length)];
        }

        // Phương thức đăng nhập
        public async Task<string> LoginAsync(string mssv, string password)
        {
            var loginData = new StringContent($"username={mssv}&password={password}&grant_type=password", Encoding.UTF8, "application/x-www-form-urlencoded");

            // Thêm các header cần thiết
            loginData.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

            try
            {
                // Gửi yêu cầu đăng nhập
                HttpResponseMessage response = await client.PostAsync("/api/auth/login", loginData);

                // Kiểm tra nếu phản hồi thành công
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Login Successful!");
                    return responseBody; // Trả về thông tin đăng nhập (token hoặc thông báo thành công)
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Login Failed: " + errorResponse);
                    return "❌ Đăng nhập thất bại. Kiểm tra MSSV & mật khẩu.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return "❌ Đã có lỗi xảy ra khi đăng nhập.";
            }
        }
    }
}

