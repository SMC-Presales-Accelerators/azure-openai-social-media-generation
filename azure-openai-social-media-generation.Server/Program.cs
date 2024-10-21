using Azure.AI.OpenAI;
using azure_openai_social_media_generation.Server;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddAzureClients(clientBuilder =>
{
    string? openaiEndpoint = builder.Configuration.GetValue<String>("AZURE_OPENAI_ENDPOINT");
    string? openaiKey = builder.Configuration.GetValue<String>("AZURE_OPENAI_API_KEY");

    if (openaiEndpoint == null || openaiKey == null)
    {
        throw new InvalidOperationException("OpenAI not configured");
    }

    string? openaiDalleEndpoint = builder.Configuration.GetValue<String>("AZURE_OPENAI_DALLE_ENDPOINT");
    string? openaiDalleKey = builder.Configuration.GetValue<String>("AZURE_OPENAI_DALLE_API_KEY");

    if (openaiDalleEndpoint == null || openaiDalleKey == null)
    {
        throw new InvalidOperationException("OpenAI Image Generation Endpoint not configured");
    }

    clientBuilder.AddOpenAIClient(new Uri(openaiEndpoint), new Azure.AzureKeyCredential(openaiKey));
    clientBuilder.AddOpenAIClient(new Uri(openaiDalleEndpoint), new Azure.AzureKeyCredential(openaiDalleKey)).WithName("OpenAiDalle");

    string? blobConnString = builder.Configuration.GetValue<String>("AZURE_BLOB_STORAGE_CONNECTION_STRING");
    if (blobConnString == null)
    {
        string? blobUrl = builder.Configuration.GetValue<String>("AZURE_BLOB_STORAGE_URL");
        string blobUrlNotNull = "";
        if (blobUrl == null) {
            throw new InvalidOperationException("Blob Storage not configured");
        } else
        {
            blobUrlNotNull = blobUrl;
        }
        clientBuilder.AddBlobServiceClient(new Uri(blobUrlNotNull));
    }
    else
    {
        clientBuilder.AddBlobServiceClient(blobConnString);
    }
    clientBuilder.UseCredential(new DefaultAzureCredential());
});
builder.Services.AddHttpClient<IImagePrepService, ImagePrepService>();

var app = builder.Build();

string? basePath = "/";
if (app.Configuration.GetValue<String>("SOCIAL_GENERATOR_BASE_PATH") != null)
{
    basePath = app.Configuration.GetValue<String>("SOCIAL_GENERATOR_BASE_PATH");
}

app.UsePathBase(basePath);

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

#pragma warning disable CS8604 //  Can't be null because of instantiation above
Uri dalleUri = new Uri(app.Configuration.GetValue<String>("AZURE_OPENAI_DALLE_ENDPOINT"));
IPHostEntry DalleEndpointCname = await Dns.GetHostEntryAsync(dalleUri.Host);
// Currently Dalle3 is only available in Sweden Central, verify we are in swedencentral 
// by checking the second part of the hostname which will designate the region
if (DalleEndpointCname.HostName.Split(".")[1] != "swedencentral")
{
    throw new InvalidOperationException("OpenAI DALL-E 3 Endpoint must be in Sweden Central");
}
#pragma warning restore CS8604 // Possible null reference argument

app.MapPost($"{basePath}getcolortheme", async (ImageUri image, IImagePrepService imagePrepService, OpenAIClient openai) =>
{
    List<string> response = await imagePrepService.GetColorThemeAsync(image.ForegroundImageUri);

    return new { DominantColors = string.Join(", ", response.ToArray()) };
})
.WithName("GetColorTheme")
.WithOpenApi();

app.MapPost($"{basePath}getbackgrounddescription", async (CopyAndImage copyAndImage, OpenAIClient openai) =>
{
    string? deploymentName = app.Configuration.GetValue<String>("AZURE_OPENAI_CHATGPT_DEPLOYMENT");
    if (deploymentName == null)
    {
        throw new InvalidOperationException("OpenAI Endpoint not configured");
    }
    string instructionsPrompt = @"You are an AI Assistant that creates descriptions of backgrounds to be used for social media marketing. You will be provided an image and marketing copy.
Provide a detailed background description that will best catch the eye of a consumer on social media. This description will be used for image generation. Use the colors of the image provided
to add colors to the background description that will make the subject really pop.";

    string prompt = "Marketing Copy: " + copyAndImage.Copy;
    var PromptMessages = new List<ChatRequestMessage>
    {
        new ChatRequestSystemMessage(instructionsPrompt),
        new ChatRequestUserMessage(new List<ChatMessageContentItem>
        {
            new ChatMessageTextContentItem(prompt),
            new ChatMessageImageContentItem(copyAndImage.ImageUrl)
        })
    };
    var ChatOptions = new ChatCompletionsOptions(messages: PromptMessages, deploymentName: deploymentName)
    {
        Temperature = (float?)0.4
    };
    ChatOptions.MaxTokens = 1500;
    ChatCompletions completions = await openai.GetChatCompletionsAsync(ChatOptions);

    return new { Description = completions.Choices[0].Message.Content };
})
.WithName("GetBackgroundDescription")
.WithOpenApi();

app.MapPost($"{basePath}generatebackgrounds", async (BackgroundDescription description, IAzureClientFactory<OpenAIClient> openAiClientFactory) =>
{
    var _openai = openAiClientFactory.CreateClient("OpenAiDalle");
    var ImageGenOptions = new ImageGenerationOptions()
    {
        DeploymentName = app.Configuration.GetValue<String>("AZURE_OPENAI_DALLE_DEPLOYMENT"),
        ImageCount = 1,
        Prompt = description.Description,
        Size = ImageSize.Size1024x1024
    };
    ImageGenerations images = await _openai.GetImageGenerationsAsync(ImageGenOptions);

    List<Uri> imageUrls = images.Data.Select(image => image.Url).ToList();

    images = await _openai.GetImageGenerationsAsync(ImageGenOptions);

    imageUrls.AddRange(images.Data.Select(image => image.Url).ToList());

    images = await _openai.GetImageGenerationsAsync(ImageGenOptions);

    imageUrls.AddRange(images.Data.Select(image => image.Url).ToList());

    return new { BackgroundUrls = imageUrls };
})
.WithName("GenerateBackgrounds")
.WithOpenApi();

app.MapPost($"{basePath}removebackgroundandcrop", async (ImageUri image, IImagePrepService imagePrepService) =>
{
    var response = await imagePrepService.CropAndRemoveBackgroundAsync(image.ForegroundImageUri);
    return new { BackgroundRemovedUrl = response };
})
.WithName("RemoveBackgroundAndCrop")
.WithOpenApi();

app.MapPost($"{basePath}combineimages", async (SocialImage images, IImagePrepService imagePrepService) =>
{
    var response = await imagePrepService.CombineImagesAsync(images.ForegroundImage, images.BackgroundImages);
    return new { CombinedImageUrls = response };
})
.WithName("CombineImages")
.WithOpenApi();

app.MapPost($"{basePath}createcopy", async (MarketingInfo info, OpenAIClient openai) =>
{
    string? deploymentName = app.Configuration.GetValue<String>("AZURE_OPENAI_CHATGPT_DEPLOYMENT");
    if (deploymentName == null)
    {
        throw new InvalidOperationException("OpenAI Endpoint not configured");
    }

    string PostTypeText = "";
    switch (info.PostType)
    {
        case "instagram":
            PostTypeText = "Instagram post";
            break;
        case "facebook":
            PostTypeText = "Facebook post";
            break;
        case "twitter":
            PostTypeText = "Tweet";
            break;
        case "linkedin":
            PostTypeText = "LinkedIn post";
            break;
        default:
            PostTypeText = "Instagram post";
            break;
    }
    string instructionsPrompt = $"You are an AI Assistant that creates an {PostTypeText}. You will be provided marketing copy and your job is to create the text for an {PostTypeText} including hashtags.";
    var PromptMessages = new List<ChatRequestMessage>
    {
        new ChatRequestSystemMessage(instructionsPrompt),
        new ChatRequestUserMessage(info.Copy)
    };
    var ChatOptions = new ChatCompletionsOptions(messages: PromptMessages, deploymentName: deploymentName)
    {
        Temperature = (float?)0.7
    };
    ChatOptions.MaxTokens = 1500;
    ChatCompletions completions = await openai.GetChatCompletionsAsync(ChatOptions);
    return new { Copy = completions.Choices[0].Message.Content };

})
.WithName("CreateSocialCopy")
.WithOpenApi();

app.MapGet($"{basePath}prepareblob", (string filename, BlobServiceClient blob) =>
{
    string extension = Path.GetExtension(filename);
    string uniqueFileName = Guid.NewGuid().ToString("N") + extension;

    string? uploadContainer = app.Configuration.GetValue<String>("AZURE_BLOB_UPLOAD_CONTAINER");
    if (uploadContainer != null)
    {
        var blobClient = blob.GetBlobContainerClient(uploadContainer).GetBlobClient(uniqueFileName);
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

            sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddDays(2);
            sasBuilder.StartsOn = DateTimeOffset.UtcNow.AddDays(-1);
            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write | BlobSasPermissions.Create);

            return new { SasUri = blobClient.GenerateSasUri(sasBuilder) };
        } else if(builder.Configuration.GetValue<String>("AZURE_BLOB_STORAGE_URL") != null)
        {
            var userDelegationKey = blob.GetUserDelegationKey(DateTimeOffset.UtcNow.AddDays(-1),
                                                                DateTimeOffset.UtcNow.AddDays(2));
            var sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = blobClient.BlobContainerName,
                BlobName = blobClient.Name,
                Resource = "b", // b for blob, c for container
                StartsOn = DateTimeOffset.UtcNow.AddDays(-1),
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(2),
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write | BlobSasPermissions.Create);

            var blobUriBuilder = new BlobUriBuilder(blobClient.Uri)
            {
                Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, blob.AccountName)
            };

            return new { SasUri = blobUriBuilder.ToUri() };
        }
        else
        {
            throw new InvalidOperationException("Server not configured");
        }
    }
    else
    {
        throw new InvalidOperationException("Server not configured");
    }
})
.WithName("PrepareBlob")
.WithOpenApi();

app.MapFallbackToFile("index.html");

app.Run();

internal record MarketingInfo(string Copy, string PostType);

internal record ImageUri(Uri ForegroundImageUri);

internal record CopyAndImage(string Copy, Uri ImageUrl);

internal record BackgroundDescription(string Description);

internal record SocialImage(Uri ForegroundImage, List<Uri> BackgroundImages);
