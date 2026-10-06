using SkiaSharp;

namespace SharedServices.Storage
{
    /// <summary>Checks that an upload really is a JPEG or PNG and strips its metadata, such as phone GPS.</summary>
    public static class ImageSanitizer
    {
        /// <summary>About 200 MB decoded, since a small file can unpack to gigabytes and crash the thumbnail maker.</summary>
        public const int MaxPixels = 50_000_000;

        public const string InvalidMessage = "Invalid image. Allowed: JPG, JPEG, PNG photos up to 50 megapixels.";

        // Tried in turn until the upright copy of a sideways photo fits the upload limit
        private static readonly int[] UprightQualities = [92, 85, 75, 65];
        private const int ShrunkQuality = 80;

        private static ReadOnlySpan<byte> EndOfImage => [0xFF, 0xD9];

        public sealed record Result(byte[] Data, string Extension);

        /// <summary>Returns the cleaned image and throws <see cref="ArgumentException"/> when it is not an acceptable photo.</summary>
        public static Result Clean(byte[] data)
        {
            using var codec = SKCodec.Create(new SKMemoryStream(data))
                ?? throw new ArgumentException(InvalidMessage);
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels)
                throw new ArgumentException(InvalidMessage);

            // The EXIF orientation flag goes with the metadata, so sideways photos are re-encoded upright first
            switch (codec.EncodedFormat)
            {
                case SKEncodedImageFormat.Jpeg:
                    return codec.EncodedOrigin == SKEncodedOrigin.TopLeft
                        ? new Result(StripJpeg(data) ?? throw new ArgumentException(InvalidMessage), ".jpg")
                        : new Result(Upright(codec), ".jpg");
                case SKEncodedImageFormat.Png:
                    return new Result(StripPng(data) ?? throw new ArgumentException(InvalidMessage), ".png");
                default:
                    throw new ArgumentException(InvalidMessage);
            }
        }

        /// <summary>True when the data carries metadata or appended data that Clean would remove.</summary>
        public static bool HasMetadata(byte[] data)
        {
            var cleaned = data.Length > 1 && data[0] == 0xFF && data[1] == 0xD8 ? StripJpeg(data)
                : data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 ? StripPng(data)
                : null;
            return cleaned != null && cleaned.Length != data.Length;
        }

        /// <summary>Re-encodes the photo with its EXIF orientation applied, never larger than an upload may be.</summary>
        private static byte[] Upright(SKCodec codec)
        {
            using var decoded = SKBitmap.Decode(codec) ?? throw new ArgumentException(InvalidMessage);
            using var upright = Orient(decoded, codec.EncodedOrigin);
            using var image = SKImage.FromBitmap(upright);

            // Cameras often save at a lower quality than ours, so the same pixels can come out larger than the upload
            foreach (var quality in UprightQualities)
            {
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality) ?? throw new ArgumentException(InvalidMessage);
                if (encoded.Size <= ImageStorage.MaxBytes) return encoded.ToArray();
            }
            return Shrink(upright);
        }

        /// <summary>Scales the photo down until it fits, for the rare one that no quality step makes small enough.</summary>
        private static byte[] Shrink(SKBitmap bitmap)
        {
            // A single pixel always fits, so the loop ends
            for (var scale = 0.8f; ; scale *= 0.8f)
            {
                var info = bitmap.Info.WithSize(Math.Max(1, (int)(bitmap.Width * scale)), Math.Max(1, (int)(bitmap.Height * scale)));
                using var smaller = bitmap.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell))
                    ?? throw new ArgumentException(InvalidMessage);
                using var image = SKImage.FromBitmap(smaller);
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, ShrunkQuality) ?? throw new ArgumentException(InvalidMessage);
                if (encoded.Size <= ImageStorage.MaxBytes) return encoded.ToArray();
            }
        }

        /// <summary>Applies an EXIF orientation, returning the same bitmap for TopLeft.</summary>
        public static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft) return bitmap;

            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var result = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
            using var canvas = new SKCanvas(result);
            switch (origin)
            {
                // Mirrored
                case SKEncodedOrigin.TopRight:
                    canvas.Translate(result.Width, 0); canvas.Scale(-1, 1); break;
                // Upside down
                case SKEncodedOrigin.BottomRight:
                    canvas.Translate(result.Width, result.Height); canvas.RotateDegrees(180); break;
                // Flipped
                case SKEncodedOrigin.BottomLeft:
                    canvas.Translate(0, result.Height); canvas.Scale(1, -1); break;
                // RightTop is the usual phone portrait
                case SKEncodedOrigin.RightTop:
                case SKEncodedOrigin.LeftTop:
                    canvas.Translate(result.Width, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.LeftBottom:
                case SKEncodedOrigin.RightBottom:
                    canvas.Translate(0, result.Height); canvas.RotateDegrees(270); break;
            }
            // Quarter turns and mirrors land on whole pixels, so the default sampling copies them exactly
            using var source = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(source, 0, 0, SKSamplingOptions.Default);
            return result;
        }

        /// <summary>Drops a JPEG's EXIF, XMP, IPTC and comment segments and whatever follows the picture, or returns null when malformed.</summary>
        private static byte[]? StripJpeg(byte[] data)
        {
            if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return null;
            using var output = new MemoryStream(data.Length);
            output.Write(data, 0, 2);
            var pos = 2;
            var hasScan = false;
            while (pos + 4 <= data.Length)
            {
                // A photo cut short still shows its upper part, so one with image data ends at the damage instead of being refused
                if (data[pos] != 0xFF)
                {
                    if (hasScan) break;
                    return null;
                }
                var marker = data[pos + 1];
                // Skip a fill byte
                if (marker == 0xFF) { pos++; continue; }
                // End of image, after which phones append video clips and further pictures
                if (marker == 0xD9) break;
                var length = (data[pos + 2] << 8) | data[pos + 3];
                if (length < 2 || pos + 2 + length > data.Length)
                {
                    if (hasScan) break;
                    return null;
                }
                // APP1 holds EXIF and XMP, APP13 IPTC and FE a comment, while the colour profile stays
                var drop = marker is 0xE1 or 0xED or 0xFE || IsPictureIndex(data, pos, length);
                if (!drop) output.Write(data, pos, 2 + length);
                pos += 2 + length;
                // Start of scan, whose compressed data runs up to the next marker
                if (marker == 0xDA)
                {
                    var end = ScanEnd(data, pos);
                    output.Write(data, pos, end - pos);
                    pos = end;
                    hasScan = true;
                }
            }
            if (!hasScan) return null;
            output.Write(EndOfImage);
            return output.ToArray();
        }

        /// <summary>The position of the first marker after a scan's compressed data, or the end of the data.</summary>
        private static int ScanEnd(byte[] data, int pos)
        {
            while (true)
            {
                var next = Array.IndexOf(data, (byte)0xFF, pos);
                if (next < 0 || next + 1 >= data.Length) return data.Length;
                var code = data[next + 1];
                // A stuffed zero and the restart markers belong to the compressed data
                if (code == 0x00 || code is >= 0xD0 and <= 0xD7) pos = next + 2;
                // A fill byte, so the next one decides
                else if (code == 0xFF) pos = next + 1;
                else return next;
            }
        }

        /// <summary>True for the APP2 multi-picture index, which only points at the pictures cut off after the first.</summary>
        private static bool IsPictureIndex(byte[] data, int pos, int length)
            => data[pos + 1] == 0xE2 && length >= 6 && data.AsSpan(pos + 4, 4).SequenceEqual("MPF\0"u8);

        /// <summary>Drops the text, EXIF and timestamp chunks of a PNG, or returns null when malformed.</summary>
        private static byte[]? StripPng(byte[] data)
        {
            if (data.Length < 8 || data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47) return null;
            using var output = new MemoryStream(data.Length);
            output.Write(data, 0, 8);
            var pos = 8;
            while (pos + 12 <= data.Length)
            {
                var length = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
                if (length < 0 || pos + 12 + (long)length > data.Length) return null;
                var type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                if (type is not ("tEXt" or "zTXt" or "iTXt" or "eXIf" or "tIME"))
                    output.Write(data, pos, 12 + length);
                pos += 12 + length;
                if (type == "IEND") return output.ToArray();
            }
            return null;
        }
    }
}
