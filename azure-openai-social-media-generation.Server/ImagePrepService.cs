using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Azure;
using Azure.AI.Vision.ImageAnalysis;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
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

        private ColorList colorvectors = new ColorList();

        private static readonly ImageAnalysisOptions cropOptions = new ImageAnalysisOptions()
        {
            SmartCropsAspectRatios = new float[] { 1.0F }
        };
        /* private static readonly ImageAnalysisOptions backgroundRemoveOptions = new ImageAnalysisOptions()
        {
            SegmentationMode = ImageSegmentationMode.BackgroundRemoval
        }; */

        private string _blobContainer;

        private ImageAnalysisClient imageAnalysisClient;

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

            imageAnalysisClient = new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));

            string? uploadContainer = configuration.GetValue<String>("AZURE_BLOB_UPLOAD_CONTAINER");
            if (uploadContainer == null)
            {
                throw new ArgumentNullException("Blob Container not configured");
            }
            _blobContainer = uploadContainer;

            _httpClient = httpClient;
            _blob = blob;

            _VisionServiceKey = key;

            _VisionServiceEndpoint = endpoint;

        }

        public async Task<List<string>> GetColorThemeAsync(Uri uri)
        {
            HttpResponseMessage responseForeground = await _httpClient.GetAsync(uri);
            using Stream foregroundStream = await responseForeground.Content.ReadAsStreamAsync();

            using SixLabors.ImageSharp.Image<Rgba32> foregroundImageSharp = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(foregroundStream);
            foregroundImageSharp.Mutate(x => x.Resize(100, 100));
            Rgba32 transparent = SixLabors.ImageSharp.Color.Transparent;
            List<Vector3> colors = new List<Vector3>();

            foregroundImageSharp.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];
                        string hex = pixel.ToHex();
                        int hexVal = Convert.ToInt32(hex.Remove(hex.Length - 2), 16);
                        if(pixel != transparent)
                        {
                            colors.Add(new Vector3(pixel.R, pixel.G, pixel.B));
                        }
                    }
                }
            });

            IDictionary<string, int> colorCounts = await colorvectors.ColorCounts(colors);

            var colorList = (from entry in colorCounts orderby entry.Value descending select entry.Key).Take(3).ToList();
            return colorList;

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
            foregroundMemoryStream.Position = 0;

            BinaryData foregroundSourceBuffer = await BinaryData.FromStreamAsync(foregroundMemoryStream);
            VisualFeatures cropVisualFeatures = VisualFeatures.SmartCrops;

            try
            {
                ImageAnalysisResult imageAnalysisResult = await imageAnalysisClient.AnalyzeAsync(foregroundSourceBuffer, visualFeatures: cropVisualFeatures, options: cropOptions);

                var box = imageAnalysisResult.SmartCrops.Values[0].BoundingBox;
                Rectangle cropRect = new Rectangle(box.X, box.Y, box.Width, box.Height);

                foregroundImageSharp.Mutate(x => x.Crop(cropRect));
                foregroundImageSharp.Mutate(x => x.Resize(1024, 1024));

                using MemoryStream croppedForegroundMemoryStream = new MemoryStream();
                await foregroundImageSharp.SaveAsPngAsync(croppedForegroundMemoryStream);
                croppedForegroundMemoryStream.Position = 0;

                var croppedContent = new StreamContent(croppedForegroundMemoryStream);
                croppedContent.Headers.Add("Content-Type", "application/octet-stream");

                var backgroundRemovalRequest = new HttpRequestMessage(HttpMethod.Post, $"{_VisionServiceEndpoint}/computervision/imageanalysis:segment?api-version=2023-02-01-preview&mode=backgroundRemoval");
                backgroundRemovalRequest.Content = croppedContent;
                backgroundRemovalRequest.Headers.Add("Ocp-Apim-Subscription-Key", _VisionServiceKey);

                var backgroundRemovedResponse = await _httpClient.SendAsync(backgroundRemovalRequest);
                backgroundRemovedResponse.EnsureSuccessStatusCode();

                using SixLabors.ImageSharp.Image backgroundRemovedImage = SixLabors.ImageSharp.Image.Load(await backgroundRemovedResponse.Content.ReadAsStreamAsync());
                using MemoryStream backgroundRemovedMemoryStream = new MemoryStream();
                await backgroundRemovedImage.SaveAsPngAsync(backgroundRemovedMemoryStream);
                return await UploadToBlobStorageAsync(backgroundRemovedMemoryStream);

            }
            catch (RequestFailedException e)
            {
                if (e.Status != 200)
                {
                    Console.WriteLine("Error analyzing image.");
                    Console.WriteLine($"HTTP status code {e.Status}: {e.Message}");
                    throw;
                }
                else
                {
                    throw;
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("Error Removing Background.");
                Console.WriteLine($"HTTP status code {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
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
