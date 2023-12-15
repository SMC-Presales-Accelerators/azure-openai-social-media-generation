using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Azure.AI.Vision.Common;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using SixLabors.ImageSharp.PixelFormats;
using Newtonsoft.Json;
using System.Text;

namespace azure_openai_social_media_generation.Server
{
    interface IImagePrepService
    {
        Task<List<string>> GetColorThemeAsync(Uri uri);
        Task<Uri> CropAndRemoveBackgroundAsync(Uri foregroundImage);
        Task<List<Uri>> CombineImagesAsync(Uri ForegroundImage, List<Uri> BackgroundImages);
    }
    public class ImagePrepService : IImagePrepService
    {
        private HttpClient _httpClient;
        private BlobServiceClient _blob;

        private static readonly ImageAnalysisOptions cropOptions = new ImageAnalysisOptions()
        {
            Features = ImageAnalysisFeature.CropSuggestions,
            CroppingAspectRatios = new List<double> { 1.0 }
        };
        private static readonly ImageAnalysisOptions backgroundRemoveOptions = new ImageAnalysisOptions()
        {
            SegmentationMode = ImageSegmentationMode.BackgroundRemoval
        };

        private string _blobContainer;

        private VisionServiceOptions serviceOptions;

        private string _VisionServiceEndpoint;
        private string _VisionServiceKey;

        public ImagePrepService(IConfiguration configuration, HttpClient httpClient, BlobServiceClient blob)
        {
            string? key = configuration.GetValue<string>("VISION_SERVICE_KEY");
            string? endpoint = configuration.GetValue<string>("VISION_SERVICE_ENDPOINT");
            if (key == null || endpoint == null)
            {
                throw new ArgumentNullException("Vision Service not configured");
            }
            string? uploadContainer = configuration.GetValue<String>("AZURE_BLOB_UPLOAD_CONTAINER");
            if (uploadContainer == null)
            {
                throw new ArgumentNullException("Blob Container not configured");
            }
            _blobContainer = uploadContainer;
            serviceOptions = new VisionServiceOptions(
            endpoint, 
            new AzureKeyCredential(key));
            _httpClient = httpClient;
            _blob = blob;

            _VisionServiceKey = key;

            _VisionServiceEndpoint = endpoint;

        }

        public async Task<List<string>> GetColorThemeAsync(Uri uri)
        {
            // Here we are using the v3.1 endpoint for the Computer Vision API instead of 4.0 because the 4.0 endpoint does not support color analysis
            // We are manually calling it to get color analysis, as the SDK drops the color analysis from the response
            string ColorAnalysisEndpoint = $"{_VisionServiceEndpoint}/vision/v3.1/analyze?visualFeatures=Color";
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(ColorAnalysisEndpoint),
                Headers = {
                    { "Ocp-Apim-Subscription-Key", _VisionServiceKey }
                },
                Content = new StringContent(JsonConvert.SerializeObject(new {url = uri}), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage responseColor = await _httpClient.SendAsync(httpRequestMessage);

            string color_response = await responseColor.Content.ReadAsStringAsync();
            AzureColorResponse color = JsonConvert.DeserializeObject<AzureColorResponse>(color_response);
            if(color == null || color!.color == null || color!.color!.dominantColors == null || color!.color!.accentColor == null)
            {
                throw new ApplicationException("Unable to analyze image");
            }
            List<string> colors = color!.color!.dominantColors.ToList();
            colors.Add(color!.color!.accentColor);
            return colors;
            
        }

        public async Task<Uri> CropAndRemoveBackgroundAsync(Uri foregroundImage)
        {
            if (foregroundImage == null)
                throw new ArgumentNullException("Pass a foreground image url");

            HttpResponseMessage responseForeground = await _httpClient.GetAsync(foregroundImage);
            using Stream foregroundStream = await responseForeground.Content.ReadAsStreamAsync();

            using SixLabors.ImageSharp.Image foregroundImageSharp = await SixLabors.ImageSharp.Image.LoadAsync(foregroundStream);

            using MemoryStream foregroundMemoryStream = new MemoryStream();
            await foregroundImageSharp.SaveAsPngAsync(foregroundMemoryStream);
            using var foregroundSourceBuffer = new ImageSourceBuffer();
            foregroundSourceBuffer.GetWriter().Write(foregroundMemoryStream.ToArray());
            var foregroundVisionSource = VisionSource.FromImageSourceBuffer(foregroundSourceBuffer);

            using var cropAnalyzer = new ImageAnalyzer(serviceOptions, foregroundVisionSource, cropOptions);

            var cropResult = cropAnalyzer.Analyze();

            if (cropResult.Reason == ImageAnalysisResultReason.Analyzed)
            {
                var box = cropResult.CropSuggestions[0].BoundingBox;
                Rectangle cropRect = new Rectangle(box.X, box.Y, box.Width, box.Height);

                foregroundImageSharp.Mutate(x => x.Crop(cropRect));
                foregroundImageSharp.Mutate(x => x.Resize(1024, 1024));

                using MemoryStream croppedForegroundMemoryStream = new MemoryStream();
                await foregroundImageSharp.SaveAsPngAsync(croppedForegroundMemoryStream);

                using var croppedForegroundSourceBuffer = new ImageSourceBuffer();
                croppedForegroundSourceBuffer.GetWriter().Write(croppedForegroundMemoryStream.ToArray());
                var croppedForegroundVisionSource = VisionSource.FromImageSourceBuffer(croppedForegroundSourceBuffer);

                using var backgroundRemoveAnalyzer = new ImageAnalyzer(serviceOptions, croppedForegroundVisionSource, backgroundRemoveOptions);

                var backgroundRemovalResult = backgroundRemoveAnalyzer.Analyze();
                if (backgroundRemovalResult.Reason == ImageAnalysisResultReason.Analyzed)
                {
                    using var segmentationResult = backgroundRemovalResult.SegmentationResult;

                    var transparentForegroundImageBuffer = segmentationResult.ImageBuffer;

                    using SixLabors.ImageSharp.Image backgroundRemovedImage = SixLabors.ImageSharp.Image.Load(transparentForegroundImageBuffer.ToArray());
                    using MemoryStream backgroundRemovedMemoryStream = new MemoryStream();
                    await backgroundRemovedImage.SaveAsPngAsync(backgroundRemovedMemoryStream);
                    return await UploadToBlobStorageAsync(backgroundRemovedMemoryStream);
                }
                else
                {
                    var errorDetails = ImageAnalysisErrorDetails.FromResult(backgroundRemovalResult);
                    throw new ApplicationException(errorDetails.Message);
                }
            }
            else
            {
                var errorDetails = ImageAnalysisErrorDetails.FromResult(cropResult);
                throw new ApplicationException(errorDetails.Message);
            }
        }

        public async Task<List<Uri>> CombineImagesAsync(Uri ForegroundImage, List<Uri> BackgroundImages)
        {
            if (ForegroundImage == null || BackgroundImages.Count == 0)
                throw new ArgumentNullException("Pass both a background and foreground image url");

            List<Uri> combinedImages = new List<Uri>();

            HttpResponseMessage responseForeground = await _httpClient.GetAsync(ForegroundImage);
            using Stream foregroundStream = await responseForeground.Content.ReadAsStreamAsync();

            using Image<Rgba32> foregroundImageSharp = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(foregroundStream);

            int EdgeFlags = 0;

            // Check which pixels are transparent to determine where to place the foreground image
            // Using bit flags to track which edges are transparent
            // Top = 8, Right = 4, Bottom = 2, Left = 1

            foregroundImageSharp.ProcessPixelRows(accessor =>
            {
                Rgba32 transparent = SixLabors.ImageSharp.Color.Transparent;
                int TopEdgeTotal = 0;
                int BottomEdgeTotal = 0;
                int LeftEdgeTotal = 0;
                int RightEdgeTotal = 0;

                int pixelOffset = 10;
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];
                        if(y == pixelOffset)
                        {
                            TopEdgeTotal += pixel.A;
                        }
                        if(y == accessor.Height - pixelOffset)
                        {
                            BottomEdgeTotal += pixel.A;
                        }
                        if(x == pixelOffset)
                        {
                            LeftEdgeTotal += pixel.A;
                        }
                        if(x == accessor.Width - pixelOffset)
                        {
                            RightEdgeTotal += pixel.A;
                        }
                    }
                }

                EdgeFlags |= TopEdgeTotal == 0 ? (1 << 3) : (0 << 3);
                EdgeFlags |= RightEdgeTotal == 0 ? (1 << 2) : (0 << 2);
                EdgeFlags |= BottomEdgeTotal == 0 ? (1 << 1) : (0 << 1);
                EdgeFlags |= LeftEdgeTotal == 0 ? (1 << 0) : (0 << 0);
            });

            Point position = new Point(0, 0);

            switch (EdgeFlags)
            {
                // If all edges are transparent, center the image
                case 0:
                    foregroundImageSharp.Mutate(x => x.Resize(724, 724));
                    position = new Point(150, 150);
                    break;
                // If top and right edges are transparent, place the image in the bottom left
                case 12:
                    foregroundImageSharp.Mutate(x => x.Resize(724, 724));
                    position = new Point(0, 300);
                    break;
                 // If the top/left transparent, place the image in the bottom right
                 case 9:
                    foregroundImageSharp.Mutate(x => x.Resize(724, 724));
                    position = new Point(300, 300);
                    break;
                // If the bottom/right transparent, place the image in the top left
                case 6:
                    foregroundImageSharp.Mutate(x => x.Resize(724, 724));
                    position = new Point(0, 0);
                    break;
                // If the bottom/left transparent, place the image in the top right
                case 3:
                    foregroundImageSharp.Mutate(x => x.Resize(724, 724));
                    position = new Point(300, 0);
                    break;
            }


            foreach (Uri BackgroundImage in BackgroundImages)
            {
                HttpResponseMessage responseBackground = await _httpClient.GetAsync(BackgroundImage);
                using Stream backgroundStream = await responseBackground.Content.ReadAsStreamAsync();

                SixLabors.ImageSharp.Image backgroundImageSharp = await SixLabors.ImageSharp.Image.LoadAsync(backgroundStream);
                backgroundImageSharp.Mutate(x => x.DrawImage(foreground: foregroundImageSharp, backgroundLocation: position, opacity: 1));

                using MemoryStream finalImageMemoryStream = new MemoryStream();
                await backgroundImageSharp.SaveAsPngAsync(finalImageMemoryStream);
                combinedImages.Add(await UploadToBlobStorageAsync(finalImageMemoryStream));
            }

            return combinedImages;
        }

        public async Task<Uri> CreateImageAsync(Uri foregroundImage, Uri backgroundImage)
        {
            if (foregroundImage == null || backgroundImage == null)
                throw new ArgumentNullException("Pass both a background and foreground image url");

            HttpResponseMessage responseBackground = await _httpClient.GetAsync(backgroundImage);
            Stream backgroundStream = await responseBackground.Content.ReadAsStreamAsync();
            HttpResponseMessage responseForeground = await _httpClient.GetAsync(foregroundImage);
            Stream foregroundStream = await responseForeground.Content.ReadAsStreamAsync();

            SixLabors.ImageSharp.Image foregroundImageSharp = await SixLabors.ImageSharp.Image.LoadAsync(foregroundStream);
            SixLabors.ImageSharp.Image backgroundImageSharp = await SixLabors.ImageSharp.Image.LoadAsync(backgroundStream);

            using MemoryStream foregroundMemoryStream = new MemoryStream();
            await foregroundImageSharp.SaveAsPngAsync(foregroundMemoryStream);
            using var foregroundSourceBuffer = new ImageSourceBuffer();
            foregroundSourceBuffer.GetWriter().Write(foregroundMemoryStream.ToArray());
            var foregroundVisionSource = VisionSource.FromImageSourceBuffer(foregroundSourceBuffer);

            using var cropAnalyzer = new ImageAnalyzer(serviceOptions, foregroundVisionSource, cropOptions);

            var cropResult = cropAnalyzer.Analyze();

            if (cropResult.Reason == ImageAnalysisResultReason.Analyzed)
            {
                var box = cropResult.CropSuggestions[0].BoundingBox;
                Rectangle cropRect = new Rectangle(box.X, box.Y, box.Width, box.Height);

                foregroundImageSharp.Mutate(x => x.Crop(cropRect));
                foregroundImageSharp.Mutate(x => x.Resize(1024, 1024));

                using MemoryStream croppedForegroundMemoryStream = new MemoryStream();
                await foregroundImageSharp.SaveAsPngAsync(croppedForegroundMemoryStream);

                using var croppedForegroundSourceBuffer = new ImageSourceBuffer();
                croppedForegroundSourceBuffer.GetWriter().Write(croppedForegroundMemoryStream.ToArray());
                var croppedForegroundVisionSource = VisionSource.FromImageSourceBuffer(croppedForegroundSourceBuffer);

                using var backgroundRemoveAnalyzer = new ImageAnalyzer(serviceOptions, croppedForegroundVisionSource, backgroundRemoveOptions);

                var backgroundRemovalResult = backgroundRemoveAnalyzer.Analyze();
                if (backgroundRemovalResult.Reason == ImageAnalysisResultReason.Analyzed)
                {
                    using var segmentationResult = backgroundRemovalResult.SegmentationResult;

                    var transparentForegroundImageBuffer = segmentationResult.ImageBuffer;

                    SixLabors.ImageSharp.Image backgroundRemovedImage = SixLabors.ImageSharp.Image.Load(transparentForegroundImageBuffer.ToArray());
                    backgroundImageSharp.Mutate(x => x.DrawImage(backgroundRemovedImage, 1));

                    using MemoryStream finalImageMemoryStream = new MemoryStream();
                    await backgroundImageSharp.SaveAsPngAsync(finalImageMemoryStream);
                    return await UploadToBlobStorageAsync(finalImageMemoryStream);
                }
                else
                {
                    var errorDetails = ImageAnalysisErrorDetails.FromResult(backgroundRemovalResult);
                    throw new ApplicationException(errorDetails.Message);
                }
            }
            else
            {
                var errorDetails = ImageAnalysisErrorDetails.FromResult(cropResult);
                throw new ApplicationException(errorDetails.Message);
            }
            
        }

        private async Task<Uri> UploadToBlobStorageAsync(Stream stream)
        {
            string uniqueFileName = Guid.NewGuid().ToString("N") + ".png";
            var blobClient = _blob.GetBlobContainerClient(_blobContainer).GetBlobClient(uniqueFileName);
            stream.Position = 0;
            await blobClient.UploadAsync(stream);
            // Check if BlobContainerClient object has been authorized with Shared Key
            if (blobClient.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one day
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                    BlobName = blobClient.Name,
                    Resource = "b"
                };

                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddDays(1);
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                return blobClient.GenerateSasUri(sasBuilder);
            }
            else
            {
                throw new InvalidOperationException("Server not configured");
            }
        }
    }
}
