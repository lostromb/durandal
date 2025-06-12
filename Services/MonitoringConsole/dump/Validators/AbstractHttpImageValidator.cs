using Photon.Common.Util;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils.Tasks;
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
    /// Base class for an HTTP validator which treats the entire HTTP response as an image file, and compares it graphically against a baseline
    /// </summary>
    public abstract class AbstractHttpImageValidator : IHttpResponseValidator
    {
        private const float DIVERGENCE_THRESHOLD = 0.03f;

        private readonly Bitmap _expectedBitmap;
        private readonly bool _allowTransparentImage;

        /// <summary>
        /// Constructs a new image validator
        /// </summary>
        /// <param name="expectedBitmap">If non-null, the response image will be expected to have 97% similarity with this bitmap, and match both width and height exactly</param>
        /// <param name="allowTransparent">If true, a completely transparent response image is acceptable</param>
        public AbstractHttpImageValidator(Bitmap expectedBitmap = null, bool allowTransparent = false)
        {
            _expectedBitmap = expectedBitmap;
            _allowTransparentImage = allowTransparent;
        }
        
        public async Task<SingleTestResult> Validate(HttpResponse response)
        {
            await DurandalTaskExtensions.NoOpTask;

            using (MemoryStream imageStream = new MemoryStream(response.PayloadData, false))
            {
                Bitmap renderedImage = Image.FromStream(imageStream) as Bitmap;
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
                        ErrorMessage = "Image divergence of " + divergence + " is greater than threshold of " + DIVERGENCE_THRESHOLD + ". Please check that the response image looks correct"
                    };
                }

                return new SingleTestResult()
                {
                    Success = true
                };
            }
        }
    }
}
