namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Placeholder cho endpoint chưa hiện thực.
/// Mọi endpoint trong skeleton trả 501 kèm mã FR và người phụ trách, để:
///   - Scalar UI (/scalar) hiển thị đầy đủ ~30 endpoint ngay từ ngày đầu
///   - Frontend biết trước shape của route mà làm việc song song
///   - Không ai nhầm endpoint rỗng là đã xong
/// Thay thân hàm bằng mediator.Send(...) khi làm tới FR tương ứng, rồi xóa lời gọi này.
/// </summary>
public static class NotImplementedResults
{
    public static IResult Pending(string frCode, string owner) =>
        Results.Problem(
            title: "Chưa hiện thực",
            detail: $"{frCode} chưa được hiện thực. Người phụ trách: {owner}. Xem docs/traceability.md.",
            statusCode: StatusCodes.Status501NotImplemented,
            type: "NOT_IMPLEMENTED");
}
