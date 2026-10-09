using System.Net.Http.Json;

namespace SRT.Complaint.Services;

// เฉพาะเรื่องร้องเรียนทั่วไป (dbo.*) ห้ามใช้กับเรื่องทุจริต — ข้อมูลผู้แจ้งทุจริตต้องไม่ออกนอกระบบไปยัง Discord
public class DiscordNotifier(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<DiscordNotifier> logger) : IDiscordNotifier
{
    public async Task NotifyNewComplaintAsync(NewComplaintDiscordPayload payload, CancellationToken ct = default)
    {
        var webhookUrl = config["Discord:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl)) return;

        try
        {
            var body = new
            {
                embeds = new object[]
                {
                    new
                    {
                        title = $"📨 เรื่องร้องเรียนใหม่ {payload.ReferenceNumber}",
                        color = PriorityColor(payload.Priority),
                        fields = new object[]
                        {
                            new { name = "หมวดหมู่", value = payload.CategoryName, inline = true },
                            new { name = "ความสำคัญ", value = PriorityLabel(payload.Priority), inline = true },
                            new { name = "สถานี/พื้นที่", value = string.IsNullOrWhiteSpace(payload.SubjectStation) ? "-" : payload.SubjectStation, inline = true }
                        },
                        timestamp = payload.CreatedAt.ToString("o")
                    }
                }
            };

            var client = httpClientFactory.CreateClient();
            using var response = await client.PostAsJsonAsync(webhookUrl, body, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Discord webhook returned {Status} for {Ref}", response.StatusCode, payload.ReferenceNumber);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send Discord notification for {Ref}", payload.ReferenceNumber);
        }
    }

    private static int PriorityColor(string p) => p switch
    {
        "Critical" => 0xE53E3E,
        "Urgent"   => 0xDD6B20,
        "High"     => 0xD69E2E,
        "Normal"   => 0x3182CE,
        _          => 0x718096
    };

    private static string PriorityLabel(string p) => p switch
    {
        "Critical" => "เร่งด่วนมาก",
        "Urgent"   => "เร่งด่วน",
        "High"     => "สำคัญ",
        "Normal"   => "ปกติ",
        "Low"      => "ข้อเสนอแนะ",
        _          => p
    };
}
