using Photon.Common.Util;
using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Photon.Common.Schemas;

namespace Photon.Common.Validators
{
    /// <summary>
    /// Base class for an HTTP validator which extracts a URL from the response, downloads that URL as an image file, and then
    /// optionally compares that image against a baseline
    /// </summary>
    public abstract class AbstractHttpIndirectImageValidator : IHttpResponseValidator
    {
        private const float DIVERGENCE_THRESHOLD = 0.03f;
        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly Bitmap _expectedBitmap;
        private readonly bool _allowTransparentImage;

        /// <summary>
        /// Constructs a new image validator
        /// </summary>
        /// <param name="expectedBitmap">If non-null, the response image will be expected to have 97% similarity with this bitmap, and match both width and height exactly</param>
        /// <param name="allowTransparent">If true, a completely transparent response image is acceptable</param>
        public AbstractHttpIndirectImageValidator(Bitmap expectedBitmap = null, bool allowTransparent = false)
        {
            _expectedBitmap = expectedBitmap;
            _allowTransparentImage = allowTransparent;
        }

        /// <summary>
        /// Processes an HTTP response and extracts an image URL to be checked.
        /// This is optional if responseIsImageData was set in the constructor, then there's no extra URL fetch the image is just already here
        /// </summary>
        /// <param name="responseMessage"></param>
        /// <param name="responseContent"></param>
        /// <returns>The URL of the image to be downloaded and validated</returns>
        protected virtual Uri ExtractImageUrl(HttpResponse response)
        {
            return null;
        }

        public async Task<SingleTestResult> Validate(HttpResponse response)
        {
            Uri imageUri = ExtractImageUrl(response);
            if (imageUri == null)
            {
                string responseString = response.GetPayloadAsString();

                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Could not find image URL in HTTP response. Response was " + responseString
                };
            }

            Bitmap renderedImage = await ImageUtil.DownloadImage(_httpClient, imageUri);
            if (renderedImage == null)
            {
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Rendered image was null"
                };
            }

            return ValidateImage(renderedImage, imageUri);
        }

        private SingleTestResult ValidateImage(Bitmap renderedImage, Uri imageUri)
        {
            if (!_allowTransparentImage)
            {
                if (ImageUtil.IsEntirelyTransparent(renderedImage))
                {
                    return new SingleTestResult()
                    {
                        Success = false,
                        ErrorMessage = "Rendering service returned a transparent image"
                    };
                }
            }

            if (renderedImage.Width != _expectedBitmap.Width || renderedImage.Height != _expectedBitmap.Height)
            {
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Rendered image had incorrect dimensions"
                };
            }

            // Now compare it to the expected image
            float divergence = ImageUtil.CalculateImageDivergence(renderedImage, _expectedBitmap);
            if (divergence > DIVERGENCE_THRESHOLD)
            {
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "Image divergence of " + divergence + " is greater than threshold of " + DIVERGENCE_THRESHOLD + ". Please check that the response image looks correct: " + imageUri
                };
            }

            return new SingleTestResult()
            {
                Success = true
            };
        }
    }
}
