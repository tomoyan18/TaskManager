using TaskManager.Application.Abstractions;

namespace TaskManager.Infrastructure.Storage;

/// <summary>
/// 添付ファイルの種類判定（domain-model.md A-02）。拡張子の許可リストと、先頭バイト（マジックナンバー）の両方で確認する。
/// </summary>
public sealed class AttachmentContentInspector : IAttachmentContentInspector
{
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif = "GIF8"u8.ToArray();
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];

    public AttachmentInspection Inspect(string fileName, ReadOnlySpan<byte> header)
    {
#pragma warning disable CA1308 // 拡張子の比較用
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
#pragma warning restore CA1308
        return extension switch
        {
            ".pdf" => Check(header.StartsWith(Pdf), "application/pdf"),
            ".png" => Check(header.StartsWith(Png), "image/png"),
            ".jpg" or ".jpeg" => Check(header.StartsWith(Jpeg), "image/jpeg"),
            ".gif" => Check(header.StartsWith(Gif), "image/gif"),
            ".webp" => Check(header.Length >= 12 && header[..4].SequenceEqual(Riff) && header[8..12].SequenceEqual(Webp), "image/webp"),
            ".zip" => Check(header.StartsWith(Zip), "application/zip"),
            ".docx" => Check(header.StartsWith(Zip), "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            ".xlsx" => Check(header.StartsWith(Zip), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            ".pptx" => Check(header.StartsWith(Zip), "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
            ".txt" => Check(IsText(header), "text/plain"),
            ".csv" => Check(IsText(header), "text/csv"),
            ".md" => Check(IsText(header), "text/markdown"),
            _ => new AttachmentInspection(false, null),
        };
    }

    private static AttachmentInspection Check(bool ok, string contentType) => new(ok, ok ? contentType : null);

    /// <summary>テキストとみなせるか（NUL を含まず、制御文字がタブ・改行のみ）。</summary>
    private static bool IsText(ReadOnlySpan<byte> header)
    {
        foreach (var b in header)
        {
            if (b == 0 || (b < 0x20 && b is not (byte)'\t' and not (byte)'\n' and not (byte)'\r' and not 0x0C and not 0x1B))
            {
                return false;
            }
        }

        return true;
    }
}
