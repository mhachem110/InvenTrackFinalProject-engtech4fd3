using SkiaSharp;

namespace InvenTrack.Utilities
{
    public static class ResizeImage
    {

        /// <summary>
        /// Code by David Stovell from ideas I found in various posts on the internet.
        /// NOTE: This code maintains aspect ratio so for example, a tall, narrow image will 
        ///      shrink until the height matches max_height but the width will be smaller then max_width.
        /// NOTE: If the image is smaller it will not be enlarged.
        /// USE THIS METHOD: if you just want a WebP Image at full quality without extra parameters
        /// </summary>
        /// <param name="originalImage">Byte Array from the uploaded file</param>
        /// <param name="max_height">Default 100</param>
        /// <param name="max_width">Default 120</param>
        /// <returns>Byte Array of the resized image - MIME Type "image/webp"</returns>
        public static Byte[] shrinkImageWebp(Byte[] originalImage,
            int max_height = 100, int max_width = 120)
        {
            using SKMemoryStream sourceStream = new SKMemoryStream(originalImage);
            using SKCodec codec = SKCodec.Create(sourceStream);
            sourceStream.Seek(0);

            using SKImage image = SKImage.FromEncodedData(SKData.Create(sourceStream));
            int newHeight = image.Height;
            int newWidth = image.Width;

            if (max_height > 0 && newHeight > max_height)
            {
                double scale = (double)max_height / newHeight;
                newHeight = max_height;
                newWidth = (int)Math.Floor(newWidth * scale);
            }

            if (max_width > 0 && newWidth > max_width)
            {
                double scale = (double)max_width / newWidth;
                newWidth = max_width;
                newHeight = (int)Math.Floor(newHeight * scale);
            }

            var info = codec.Info.ColorSpace.IsSrgb ? new SKImageInfo(newWidth, newHeight) : new SKImageInfo(newWidth, newHeight, SKImageInfo.PlatformColorType, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
            using SKSurface surface = SKSurface.Create(info);
            using SKPaint paint = new SKPaint();
            // High quality without antialiasing
            paint.IsAntialias = true;
            paint.FilterQuality = SKFilterQuality.High;

            // Draw the bitmap to fill the surface
            surface.Canvas.Clear(SKColors.White);
            var rect = new SKRect(0, 0, newWidth, newHeight);
            surface.Canvas.DrawImage(image, rect, paint);
            surface.Canvas.Flush();

            using SKImage newImage = surface.Snapshot();
            using SKData newImageData = newImage.Encode(SKEncodedImageFormat.Webp, 100);

            return newImageData.ToArray();
        }
    }
}
