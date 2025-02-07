using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using JWT;
using JWT.Serializers;
using System.Net.Http.Headers;

namespace BOT_TKB_TVU
{
    public class Program
    {
        public static readonly string API_URL = "https://ttsv.tvu.edu.vn";
        public static readonly Dictionary<long, string> userTokens = new();
        private static readonly Dictionary<long, (string mssv, string password)> userCredentials = new();
        public static readonly Dictionary<long, DateTime> tokenExpirationTimes = new();
        private static ITelegramBotClient botClient = new TelegramBotClient("7280126902:AAGpOWP1R0vvGxAR9_gDL5bWk4zRpsd0ouo");
        private static IScheduler scheduler;

        public static async Task Main()
        {
            scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            await scheduler.Start();
            botClient.StartReceiving(
                HandleUpdateAsync, // Xử lý cập nhật
                HandleErrorAsync // Xử lý lỗi
            );
            ScheduleTokenRefreshJob();
            Console.WriteLine("Bot đang chạy...");
            Console.ReadLine();
        }

        // Xử lý các cập nhật
        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message != null && update.Message.Text != null)
            {
                // Xử lý tin nhắn
                await HandleMessage(update);
            }
        }

        // Xử lý tin nhắn
        private static async Task HandleMessage(Update update)
        {
            string messageText = update.Message.Text;
            var chatId = update.Message.Chat.Id;
            string[] args = messageText.Split(' ');

            switch (args[0])
            {
                case "/start":
                    await botClient.SendTextMessageAsync(chatId, "Chào mừng! Mình là bot hỗ trợ học tập của bạn. Hãy nhập /help để xem các lệnh hỗ trợ");
                    break;
                case "/login":
                    if (args.Length != 3)
                    {
                        await botClient.SendTextMessageAsync(chatId, "Sai cú pháp. Dùng: /login <mssv> <password>");
                        return;
                    }
                    await HandleLogin(chatId, args[1], args[2]);
                    break;
                case "/xemtkb":
                    await HandleXemTKB(chatId);
                    break;
                case "/ngaymai":
                    await HandleNgayMai(chatId);
                    break;
                case "/batthongbao":
                    await HandleBatThongBao(chatId);
                    break;
                case "/dangkyhocphan":
                    await HandleDangKyHocPhan(chatId);
                    break;
                case "/tatthongbao":
                    await HandleTatThongBao(chatId);
                    break;
                case "/help":
                    await HandleHelp(chatId);
                    break;
                default:
                    break;
            }
        }

        // Xử lý lỗi
        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Có lỗi: {exception.Message}");
            return Task.CompletedTask;
        }

        private static async Task HandleLogin(long chatId, string mssv, string password)
        {
            // Kiểm tra xem người dùng đã đăng nhập chưa
            if (userTokens.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "Bạn đã đăng nhập rồi. Không cần đăng nhập lại.");
                return;
            }

            using HttpClient client = new();

            var formData = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("username", mssv),
        new KeyValuePair<string, string>("password", password),
        new KeyValuePair<string, string>("grant_type", "password") // Thêm tham số grant_type
    });

            try
            {
                var response = await client.PostAsync($"{API_URL}/api/auth/login", formData);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                    string token = data.access_token;

                    userTokens[chatId] = token;
                    userCredentials[chatId] = (mssv, password);

                    // Lưu thời gian hết hạn của token (2 giờ kể từ lúc đăng nhập)
                    tokenExpirationTimes[chatId] = DateTime.Now.AddHours(2);

                    await botClient.SendTextMessageAsync(chatId, "✅ Đăng nhập thành công!");
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Lỗi khi đăng nhập: {errorResponse}");
                    await botClient.SendTextMessageAsync(chatId, "❌ Đăng nhập thất bại. Kiểm tra MSSV & mật khẩu.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi gửi yêu cầu: {ex.Message}");
                await botClient.SendTextMessageAsync(chatId, "❌ Đã xảy ra lỗi khi đăng nhập. Vui lòng thử lại.");
            }
        }





        public static async Task<string> RefreshToken(long chatId)
        {
            if (!userCredentials.ContainsKey(chatId))
            {
                return null;
            }

            var (mssv, password) = userCredentials[chatId];

            using HttpClient client = new();

            // Định dạng dữ liệu dưới dạng application/x-www-form-urlencoded
            var values = new Dictionary<string, string>
    {
        { "username", mssv },
        { "password", password },
        { "grant_type", "password" }
    };

            var content = new FormUrlEncodedContent(values);

            // Thêm header Content-Type
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

            try
            {
                var response = await client.PostAsync($"{API_URL}/api/auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                    string token = data.access_token;

                    userTokens[chatId] = token;
                    Console.WriteLine($"Token mới đã được cấp cho chatId {chatId}.");
                    return token;
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Lỗi khi làm mới token: {response.StatusCode} - {errorResponse}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi làm mới token: {ex.Message}");
                return null;
            }
        }



        private static async Task RefreshTokenAfterTimeout(long chatId)
        {
            // Kiểm tra nếu token đã hết hạn
            if (tokenExpirationTimes.ContainsKey(chatId) && DateTime.Now > tokenExpirationTimes[chatId])
            {
                // Làm mới token
                string newToken = await RefreshToken(chatId);
                if (newToken != null)
                {
                    // Cập nhật thời gian hết hạn của token mới
                    tokenExpirationTimes[chatId] = DateTime.Now.AddHours(2);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Phiên làm việc đã hết hạn. Hãy đăng nhập lại.");
                }
            }
        }



        private static async Task HandleXemTKB(long chatId)
        {
            if (!userTokens.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "Bạn chưa đăng nhập. Hãy dùng /login <mssv> <password>.");
                return;
            }

            string token = userTokens[chatId];
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

            // Chuyển payload sang dạng x-www-form-urlencoded
            var values = new Dictionary<string, string>
    {
        { "filter[hoc_ky]", "20242" },
        { "filter[ten_hoc_ky]", "" },
        { "additional[paging][limit]", "100" },
        { "additional[paging][page]", "1" }
    };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync($"{API_URL}/api/sch/w-locdstkbtuanusertheohocky", content);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden) // Token hết hạn
            {

                token = await RefreshToken(chatId);
                if (token == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Phiên làm việc hết hạn. Hãy đăng nhập lại.");
                    return;
                }
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                response = await client.PostAsync($"{API_URL}/api/sch/w-locdstkbtuanusertheohocky", content);
            }

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsStringAsync();
                var schedule = JsonConvert.DeserializeObject<dynamic>(data);
                string message = "";

                foreach (var week in schedule.data.ds_tuan_tkb)
                {
                    foreach (var subject in week.ds_thoi_khoa_bieu)
                    {
                        string ngayHoc = subject.ngay_hoc;
                        if (ngayHoc == DateTime.Now.ToString("yyyy-MM-dd"))
                        {
                            message += $"📚 {subject.ten_mon}\n👨‍🏫 GV: {subject.ten_giang_vien}\n🏢 Phòng: {subject.ma_phong}\n⏰ Tiết {subject.tiet_bat_dau}-{subject.so_tiet}\n\n";
                        }
                    }
                }

                await botClient.SendTextMessageAsync(chatId, string.IsNullOrEmpty(message) ? "Hôm nay không có lịch học." : message);

            }
            else
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                await botClient.SendTextMessageAsync(chatId, $"Lỗi khi lấy thời khóa biểu: {response.StatusCode} - {errorResponse}");
            }
        }



        private static async Task HandleBatThongBao(long chatId)
        {
            if (!userTokens.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "Bạn chưa đăng nhập. Hãy dùng /login <mssv> <password>.");
                return;
            }

            if (scheduler == null)
            {
                scheduler = await StdSchedulerFactory.GetDefaultScheduler();
                await scheduler.Start();
            }

            // Lên lịch gửi thông báo mỗi ngày vào lúc 7h sáng
            IJobDetail job = JobBuilder.Create<SendScheduleNotificationJob>()
                .UsingJobData("chatId", chatId.ToString())
                .WithIdentity($"NotifyJob_{chatId}")  // Đảm bảo sử dụng tên job đúng
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithDailyTimeIntervalSchedule(x => x
                    .OnEveryDay()
                    .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(5, 0))
                    .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")) // Đặt múi giờ Việt Nam
                    .WithIntervalInHours(24))
                .Build();

            // Kiểm tra và log job
            Console.WriteLine($"Scheduling job for chatId {chatId}");
            await scheduler.ScheduleJob(job, trigger);

            await botClient.SendTextMessageAsync(chatId, "✅ Thông báo tự động đã bật! Bạn sẽ nhận thông báo lịch học mỗi sáng.");
        }


        private static async Task HandleTatThongBao(long chatId)
        {
            if (scheduler != null)
            {
                var jobKey = new JobKey($"NotifyJob_{chatId}");
                Console.WriteLine($"Checking for job with key: {jobKey}");

                if (await scheduler.CheckExists(jobKey))
                {
                    await scheduler.DeleteJob(jobKey);
                    await botClient.SendTextMessageAsync(chatId, "❌ Thông báo tự động đã tắt!");
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Thông báo tự động chưa được bật.");
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Thông báo tự động chưa được bật.");
            }
        }




        // Thêm hàm xử lý lệnh /help
        private static async Task HandleHelp(long chatId)
        {
            string helpMessage =
                "Danh sách các lệnh hỗ trợ: \n" +
                "/login <mssv> <password> - Đăng nhập vào hệ thống\n" +
                "/help - Hiển thị các lệnh hỗ trợ\n" +
                "/xemtkb - Xem thời khóa biểu hôm nay\n" +
                "/dangkyhocphan - Đăng ký học phần tự động\n" +
                "/batthongbao - Bật thông báo tự động\n" +
                "/tatthongbao - Tắt thông báo tự động\n" +
                "Lưu ý: tài khoản và mật khẩu đăng nhập là tài khoản và mật khẩu đăng nhập vào cổng TTSV của bạn.\n" +
                "Nếu có vấn đề gì, hãy liên hệ admin @reisohz\n";

            await botClient.SendTextMessageAsync(chatId, helpMessage);
        }

        private static async Task HandleDangKyHocPhan(long chatId)
        {
            string dangkyMessage =
                "Chức năng đang được phát triển \n" +
                "Mọi góp ý, đóng góp ý tưởng vui lòng liên hệ @reisohz\n";

            await botClient.SendTextMessageAsync(chatId, dangkyMessage);
        }

        private static async Task HandleNgayMai(long chatId)
        {
            if (!userTokens.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "Bạn chưa đăng nhập. Hãy dùng /login <mssv> <password>.");
                return;
            }

            string token = userTokens[chatId];
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

            // Lấy ngày mai
            DateTime tomorrow = DateTime.Now.AddDays(1);
            string tomorrowDate = tomorrow.ToString("yyyy-MM-dd");

            // Chuyển payload sang x-www-form-urlencoded
            var values = new Dictionary<string, string>
    {
        { "filter[hoc_ky]", "20242" },
        { "filter[ten_hoc_ky]", "" },
        { "additional[paging][limit]", "100" },
        { "additional[paging][page]", "1" }
    };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync($"{API_URL}/api/sch/w-locdstkbtuanusertheohocky", content);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden) // Token hết hạn
            {
                token = await RefreshToken(chatId);
                if (token == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Phiên làm việc hết hạn. Hãy đăng nhập lại.");
                    return;
                }
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                response = await client.PostAsync($"{API_URL}/api/sch/w-locdstkbtuanusertheohocky", content);
            }

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsStringAsync();
                var schedule = JsonConvert.DeserializeObject<dynamic>(data);
                string message = "";

                foreach (var week in schedule.data.ds_tuan_tkb)
                {
                    foreach (var subject in week.ds_thoi_khoa_bieu)
                    {
                        string ngayHoc = subject.ngay_hoc;
                        if (ngayHoc == tomorrowDate) // Kiểm tra nếu ngày học là ngày mai
                        {
                            message += $"📚 {subject.ten_mon}\n👨‍🏫 GV: {subject.ten_giang_vien}\n🏢 Phòng: {subject.ma_phong}\n⏰ Tiết {subject.tiet_bat_dau}-{subject.so_tiet}\n\n";
                        }
                    }
                }

                await botClient.SendTextMessageAsync(chatId, string.IsNullOrEmpty(message) ? "Ngày mai không có lịch học." : message);
            }
            else
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                await botClient.SendTextMessageAsync(chatId, $"Lỗi khi lấy thời khóa biểu: {response.StatusCode} - {errorResponse}");
            }
        }


        private static async Task ScheduleTokenRefreshJob()
        {
            if (scheduler == null)
            {
                scheduler = await StdSchedulerFactory.GetDefaultScheduler();
                await scheduler.Start();
            }

            IJobDetail job = JobBuilder.Create<RefreshTokenJob>()
                .WithIdentity("TokenRefreshJob")
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithSimpleSchedule(x => x.WithIntervalInHours(1).RepeatForever()) // Lặp lại mỗi 1 giờ
                .Build();

            await scheduler.ScheduleJob(job, trigger);
        }

        public class RefreshTokenJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                foreach (var chatId in userTokens.Keys.ToList())
                {
                    await RefreshTokenAfterTimeout(chatId);
                }
            }
        }

    }
}
