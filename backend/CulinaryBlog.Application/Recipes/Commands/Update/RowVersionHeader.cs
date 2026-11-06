namespace CulinaryBlog.Application.Recipes.Commands.Update;

/// <summary>
/// D71 — đọc RowVersion từ header <c>If-Match</c>: chấp nhận base64 trần, có ngoặc kép (strong ETag) hoặc
/// tiền tố <c>W/</c>. <c>*</c> và chuỗi không phải base64 bị từ chối.
/// </summary>
public static class RowVersionHeader
{
    public static bool TryParse(string? ifMatch, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return false;
        }

        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        value = value.Trim('"');
        if (value.Length == 0)
        {
            return false;
        }

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }
}
