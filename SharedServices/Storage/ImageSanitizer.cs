using SkiaSharp;

namespace SharedServices.Storage
{
    /// <summary>
    /// Every uploaded photo passes through here before it is stored (<see cref="ImageStorage.SaveAsync"/>,
    /// approval requests):
    /// - it must really be a JPEG or PNG (the content decides, not the file name) of at most
    ///   <see cref="MaxPixels"/> pixels: a 1 MB file can unpack to tens of gigabytes and crash the
    ///   thumbnail maker;
    /// - its metadata is removed: phones write the location (GPS), device and time into EXIF, and
    ///   the photos are reachable by link from the internet. A sideways phone photo (EXIF orientation)
    ///   is turned upright first, since the flag that said so goes too; other JPEGs keep their pixels
    ///   untouched (only metadata segments are dropped).
    /// </summary>
    public static class ImageSanitizer
    {
        /// <summary>50 megapixels: above any phone's normal photo, about 200 MB to decode.</summary>
        public const int MaxPixels = 50_000_000;

        public const string InvalidMessage = "Invalid image. Allowed: JPG, JPEG, PNG photos up to 50 megapixels.";

        public sealed record Result(byte[] Data, string Extension);

        /// <summary>The cleaned image, or <see cref="ArgumentException"/> when it is not an acceptable photo.</summary>
        public static Result Clean(byte[] data)
        {
            using var codec = SKCodec.Create(new SKMemoryStream(data))
                ?? throw new ArgumentException(InvalidMessage);
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels)
                throw new ArgumentException(InvalidMessage);

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

        /// <summary>True when the data carries metadata this class removes (used by the one-time cleanup of stored photos).</summary>
        public static bool HasMetadata(byte[] data)
        {
            var cleaned = data.Length > 1 && data[0] == 0xFF && data[1] == 0xD8 ? StripJpeg(data)
                : data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 ? StripPng(data)
                : null;
            return cleaned != null && cleaned.Length != data.Length;
        }

        /// <summary>Decodes, applies the EXIF orientation and re-encodes (JPEG, quality 92).</summary>
        private static byte[] Upright(SKCodec codec)
        {
            using var decoded = SKBitmap.Decode(codec) ?? throw new ArgumentException(InvalidMessage);
            using var upright = Orient(decoded, codec.EncodedOrigin);
            using var image = SKImage.FromBitmap(upright);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 92) ?? throw new ArgumentException(InvalidMessage);
            return encoded.ToArray();
        }

        /// <summary>Applies an EXIF orientation (phones store pictures sideways plus a flag). Returns the same bitmap for TopLeft.</summary>
        public static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft) return bitmap;

            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var result = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
            using var canvas = new SKCanvas(result);
            switch (origin)
            {
                case SKEncodedOrigin.TopRight:      // mirrored
                    canvas.Translate(result.Width, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight:   // upside down
                    canvas.Translate(result.Width, result.Height); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft:    // flipped
                    canvas.Translate(0, result.Height); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop:      // rotated 90 degrees (the usual phone portrait)
                case SKEncodedOrigin.LeftTop:
                    canvas.Translate(result.Width, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.LeftBottom:    // rotated 270 degrees
                case SKEncodedOrigin.RightBottom:
                    canvas.Translate(0, result.Height); canvas.RotateDegrees(270); break;
            }
            canvas.DrawBitmap(bitmap, 0, 0);
            return result;
        }

        /// <summary>
        /// The JPEG without APP1 (EXIF, XMP), APP13 (IPTC) and comment segments; the colour profile
        /// (APP2), JFIF (APP0) and Adobe (APP14) segments and all image data stay. Null when malformed.
        /// </summary>
        private static byte[]? StripJpeg(byte[] data)
        {
            if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return null;
            using var output = new MemoryStream(data.Length);
            output.Write(data, 0, 2);
            var pos = 2;
            while (pos + 4 <= data.Length)
            {
                if (data[pos] != 0xFF) return null;
                var marker = data[pos + 1];
                if (marker == 0xFF) { pos++; continue; }               // fill byte
                if (marker == 0xDA)                                       // start of scan: the rest is image data
                {
                    output.Write(data, pos, data.Length - pos);
                    return output.ToArray();
                }
                var length = (data[pos + 2] << 8) | data[pos + 3];
                if (length < 2 || pos + 2 + length > data.Length) return null;
                var drop = marker is 0xE1 or 0xED or 0xFE;
                if (!drop) output.Write(data, pos, 2 + length);
                pos += 2 + length;
            }
            return null;
        }

        /// <summary>The PNG without its text and EXIF chunks (tEXt, zTXt, iTXt, eXIf, tIME). Null when malformed.</summary>
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
