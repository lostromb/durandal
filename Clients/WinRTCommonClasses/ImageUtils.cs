using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace DurandalWinRT
{
    public static class ImageUtils
    {
        public static async Task<BitmapSource> ConvertPngBytesToWpfImageSource(byte[] pngData)
        {
            BitmapImage image = new BitmapImage();
            IBuffer pngBuffer = WindowsRuntimeBuffer.Create(pngData, 0, pngData.Length, pngData.Length);
            IRandomAccessStream randomStream = new InMemoryRandomAccessStream();
            await randomStream.WriteAsync(pngBuffer);
            randomStream.Seek(0);
            await image.SetSourceAsync(randomStream);
            return image;

            //StbSharp.ImageReader imageReader = new StbSharp.ImageReader();
            //MemoryStream pngStream = new MemoryStream(pngData, false);
            //StbSharp.Image decodedImage = imageReader.Read(pngStream, 0);

            //// We have to premultiply the alpha for the image source to handle it properly so here we go
            //for (int c = 0; c < decodedImage.Data.Length; c += 4)
            //{
            //    byte a = decodedImage.Data[c + 3];
            //    decodedImage.Data[c] = (byte)(((int)decodedImage.Data[c] * a) >> 8); // fixed point multiplication done here
            //    decodedImage.Data[c + 1] = (byte)(((int)decodedImage.Data[c + 1] * a) >> 8);
            //    decodedImage.Data[c + 2] = (byte)(((int)decodedImage.Data[c + 2] * a) >> 8);
            //}

            //IBuffer pixelBuffer = WindowsRuntimeBuffer.Create(decodedImage.Data, 0, decodedImage.Data.Length, decodedImage.Data.Length);

            //SoftwareBitmap softwareBitmap = new SoftwareBitmap(
            //    BitmapPixelFormat.Bgra8,
            //    decodedImage.Width,
            //    decodedImage.Height,
            //    BitmapAlphaMode.Premultiplied);

            //softwareBitmap.CopyFromBuffer(pixelBuffer);

            //SoftwareBitmapSource bitmapSource = new SoftwareBitmapSource();
            //await bitmapSource.SetBitmapAsync(softwareBitmap);
            //return bitmapSource;
        }
    }
}
