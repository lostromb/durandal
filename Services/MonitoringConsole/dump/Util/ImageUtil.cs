using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Net.Http;
using System.IO;
using Durandal.Common.Utils.MathExt;

namespace Photon.Common.Util
{
    public static class ImageUtil
    {
        /// <summary>
        /// Downloads the image specified by the given url
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="imageUrl"></param>
        /// <returns></returns>
        public static async Task<Bitmap> DownloadImage(HttpClient httpClient, Uri imageUrl)
        {
            byte[] rendered_image = await httpClient.GetByteArrayAsync(imageUrl);
            if (rendered_image == null)
            {
                return null;
            }

            using (MemoryStream imageStream = new MemoryStream(rendered_image, false))
            {
                Image renderedImage = Image.FromStream(imageStream);
                return renderedImage as Bitmap;
            }
        }

        public static bool IsEntirelyTransparent(Bitmap image)
        {
            // Ensure the pixel format supports alpha
            if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format16bppArgb1555 ||
                image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb ||
                image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppPArgb ||
                image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format4bppIndexed || 
                image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed ||
                image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format64bppArgb ||
                image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format64bppPArgb)
            {
                // Check every pixel
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        Color ca = image.GetPixel(x, y);
                        if (ca.A > 0)
                        {
                            return false;
                        }
                    }
                }

                // All pixel have alpha == 0
                return true;
            }
            else
            {
                // Transparency not supported in pixel format, so false
                return false;
            }
        }
        
        /// <summary>
        /// Calculates image divergence as expressed in average RGB vector deviation per pixel between 0 and 1.
        /// e.g. comparing a black image with a white one should yield 1.0
        /// Identical images should have 0.0.
        /// Noise attributable to compression artifacts should be less than about 0.03
        /// </summary>
        /// <param name="a">First image</param>
        /// <param name="b">Second image</param>
        /// <returns>The normalized divergence value</returns>
        public static float CalculateImageDivergence(Bitmap a, Bitmap b)
        {
            if (a == null || b == null)
            {
                throw new ArgumentException("Input image is null");
            }

            if (a.Width != b.Width || a.Height != b.Height)
            {
                throw new ArgumentException("Images must be the same size");
            }

            float totalPixels = (float)a.Width * (float)a.Height;
            float returnVal = 0;
            float normalizer = new Vector3f(255, 255, 255).Magnitude;
            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    Color ca = a.GetPixel(x, y);
                    Color cb = b.GetPixel(x, y);
                    Vector3f vecA = new Vector3f(ca.R, ca.G, ca.B);
                    Vector3f vecB = new Vector3f(cb.R, cb.G, cb.B);
                    float divergence = vecA.Distance(vecB) / normalizer;
                    returnVal += divergence;
                }
            }

            return returnVal / totalPixels;
        }
    }
}
