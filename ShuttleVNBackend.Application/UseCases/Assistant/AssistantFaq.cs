namespace ShuttleVNBackend.Application.UseCases.Assistant;

/// <summary>
///     Fixed answers for questions the assistant may answer. The model picks a key; the words
///     come from here, so injected instructions cannot reshape a policy answer.
/// </summary>
public static class AssistantFaq
{
    public const string OpeningHours = "opening_hours";
    public const string SlotLength = "slot_length";
    public const string Pricing = "pricing";
    public const string Cancellation = "cancellation";
    public const string BookingFlow = "booking_flow";
    public const string Payment = "payment";
    public const string StaffContact = "staff_contact";

    public static readonly string[] Topics =
    [
        OpeningHours, SlotLength, Pricing, Cancellation, BookingFlow, Payment, StaffContact
    ];

    private static readonly Dictionary<string, string> Answers = new(StringComparer.Ordinal)
    {
        [OpeningHours] =
            "ShuttleVN mở cửa 05:00–22:00 hằng ngày. Mỗi sân có thể có giờ mở cửa riêng, " +
            "bạn xem giờ cụ thể ở trang chi tiết sân.",
        [SlotLength] =
            "Sân đặt theo slot 30 phút, tối thiểu 30 phút. Giờ bắt đầu và kết thúc rơi vào phút 00 hoặc 30.",
        [Pricing] =
            "Giá tính theo giờ và có thể khác nhau theo khung giờ trong ngày và ngày trong tuần. " +
            "Giá hiển thị khi bạn chọn sân và khung giờ trên trang Đặt sân.",
        [Cancellation] =
            "Đơn Chờ xác nhận hoặc Đã xác nhận có thể hủy hoặc đổi lịch khi còn ít nhất 1 giờ " +
            "trước giờ bắt đầu. Đơn đã thanh toán cần nhân viên xử lý.",
        [BookingFlow] =
            "Bạn chọn sân và khung giờ, điền thông tin liên hệ, gửi đơn; đơn ở trạng thái Chờ xác nhận " +
            "và nhân viên sẽ xác nhận sau. Khách chưa có tài khoản vẫn đặt được bằng thông tin liên hệ.",
        [Payment] =
            "Thanh toán do nhân viên thu tại cơ sở. Trợ lý không thu tiền và không xác nhận thanh toán.",
        [StaffContact] =
            "Bạn liên hệ nhân viên ShuttleVN qua số điện thoại hoặc quầy lễ tân tại cơ sở để được hỗ trợ trực tiếp."
    };

    public static string Answer(string? topic)
    {
        return topic is not null && Answers.TryGetValue(topic, out var answer)
            ? answer
            : Answers[StaffContact];
    }
}