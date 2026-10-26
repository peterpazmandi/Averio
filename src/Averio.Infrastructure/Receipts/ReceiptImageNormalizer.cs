using Averio.Application.Common.Exceptions;
using Averio.Application.Receipts;
using SkiaSharp;
namespace Averio.Infrastructure.Receipts;

public sealed class ReceiptImageNormalizer : IReceiptImageNormalizer
{
    public async Task<byte[]> NormalizeAsync(Stream input, CancellationToken ct)
    {
        using var source = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (source.Length + read > ReceiptOptions.MaxUploadBytes) throw Invalid();
            await source.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        using var data = SKData.CreateCopy(source.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png)
            || codec.Info.Width < 32 || codec.Info.Height < 32 || codec.Info.Width > 12000 || codec.Info.Height > 12000
            || (long)codec.Info.Width * codec.Info.Height > 24_000_000 || codec.FrameCount > 1) throw Invalid();
        var w = codec.Info.Width;
        var h = codec.Info.Height;
        using var pixels = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(pixels.Info, pixels.GetPixels()) != SKCodecResult.Success) throw Invalid();
        ct.ThrowIfCancellationRequested();
        var rotated = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        using var normalized = new SKBitmap(new SKImageInfo(rotated ? h : w, rotated ? w : h,
            SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(normalized))
        {
            canvas.Clear(SKColors.White);
            // Apply EXIF orientation to pixels. Fresh encoding strips GPS, comments and profiles.
            var (a,b,c,d,x,y) = codec.EncodedOrigin switch {
                SKEncodedOrigin.TopRight => (-1f,0f,0f,1f,(float)w,0f),
                SKEncodedOrigin.BottomRight => (-1f,0f,0f,-1f,(float)w,(float)h),
                SKEncodedOrigin.BottomLeft => (1f,0f,0f,-1f,0f,(float)h),
                SKEncodedOrigin.LeftTop => (0f,1f,1f,0f,0f,0f),
                SKEncodedOrigin.RightTop => (0f,-1f,1f,0f,(float)h,0f),
                SKEncodedOrigin.RightBottom => (0f,-1f,-1f,0f,(float)h,(float)w),
                SKEncodedOrigin.LeftBottom => (0f,1f,-1f,0f,0f,(float)w),
                _ => (1f,0f,0f,1f,0f,0f)
            };
            canvas.SetMatrix(new SKMatrix { ScaleX=a, SkewX=b, TransX=x, SkewY=c, ScaleY=d, TransY=y, Persp2=1 });
            canvas.DrawBitmap(pixels, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        }
        using var image = SKImage.FromBitmap(normalized);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        if (encoded is null || encoded.Size > ReceiptOptions.MaxUploadBytes) throw Invalid();
        return encoded.ToArray();
    }
    private static BusinessRuleException Invalid() => new("receipt_image_invalid", "Error_ReceiptImageInvalid");
}
