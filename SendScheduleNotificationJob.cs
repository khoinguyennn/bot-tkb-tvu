using Quartz;
using Telegram.Bot;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace BOT_TKB_TVU
{

    public class SendScheduleNotificationJob : IJob
    {
        private static ITelegramBotClient botClient = new TelegramBotClient("7280126902:AAGpOWP1R0vvGxAR9_gDL5bWk4zRpsd0ouo");

        public async Task Execute(IJobExecutionContext context)
        {
            // Lấy chatId từ JobData (thông qua JobDataMap)
            long chatId = Convert.ToInt64(context.JobDetail.JobDataMap["chatId"]);

            // Kiểm tra nếu người dùng đã đăng nhập và có token
            if (!Program.userTokens.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "Bạn chưa đăng nhập. Hãy dùng /login <mssv> <password>.");
                return;
            }

            string token = Program.userTokens[chatId];
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

            // Gửi yêu cầu lấy thời khóa biểu
            var response = await client.PostAsJsonAsync($"{Program.API_URL}/api/sch/w-locdstkbtuanusertheohocky", content);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden) // Token hết hạn
            {

                token = await Program.RefreshToken(chatId);
                if (token == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Phiên làm việc hết hạn. Hãy đăng nhập lại.");
                    return;
                }
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                response = await client.PostAsync($"{Program.API_URL}/api/sch/w-locdstkbtuanusertheohocky", content);

            }



            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsStringAsync();
                var schedule = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data);
                string message = "";

                // Duyệt qua các tuần và môn học
                foreach (var week in schedule.data.ds_tuan_tkb)
                {
                    foreach (var subject in week.ds_thoi_khoa_bieu)
                    {
                        string ngayHoc = subject.ngay_hoc;
                        if (ngayHoc == DateTime.Now.ToString("yyyy-MM-dd")) // Kiểm tra lịch học hôm nay
                        {
                            message += $"📚 {subject.ten_mon}\n👨‍🏫 GV: {subject.ten_giang_vien}\n🏢 Phòng: {subject.ma_phong}\n⏰ Tiết {subject.tiet_bat_dau}-{subject.so_tiet}\n\n";
                        }
                    }
                }

                // Gửi thông báo
                await botClient.SendTextMessageAsync(chatId, string.IsNullOrEmpty(message) ? "Hôm nay không có lịch học." : message);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Lỗi khi lấy thời khóa biểu.");
            }
        }
    }
}