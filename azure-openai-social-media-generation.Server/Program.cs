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
    clientBuilder.AddOpenAIClient(new Uri(openaiEndpoint), new Azure.AzureKeyCredential(openaiKey));
    clientBuilder.AddBlobServiceClient(builder.Configuration.GetValue<String>("AZURE_BLOB_STORAGE_CONNECTION_STRING"));
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

app.MapPost($"{basePath}getcolortheme", async (ImageUri image, IImagePrepService imagePrepService, OpenAIClient openai) =>
{
    List<string> response = await imagePrepService.GetColorThemeAsync(image.ForegroundImageUri);

    string? deploymentName = app.Configuration.GetValue<String>("AZURE_OPENAI_CHATGPT_DEPLOYMENT");
    if (deploymentName == null)
    {
        throw new InvalidOperationException("OpenAI Endpoint not configured");
    }
    string instructionsPrompt = @"You are an AI assistant that provides english color names based on hex color codes. Provide the color name as a JSON response with the proper ColorName.";

    string prompt = $"Hex Color: {response[response.Count - 1]}";
    var PromptMessages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, instructionsPrompt),
        new ChatMessage(ChatRole.User, prompt)
    };
    var ChatOptions = new ChatCompletionsOptions(messages: PromptMessages, deploymentName: deploymentName)
    {
        Temperature = (float?)0.7
    };
    ChatCompletions completions = await openai.GetChatCompletionsAsync(ChatOptions);

    string accentColor = "";

    try
    {
        JsonNode? json = JsonNode.Parse(completions.Choices[0].Message.Content);
        if (json != null && json["ColorName"] != null)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference. Can't be null because of if statement
            accentColor = json["ColorName"].ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }
        
    }
    catch
    {
        accentColor = "None";
    }

    response[response.Count - 1] = accentColor;

    return new { DominantColors = string.Join(", ", response.ToArray()) };
})
.WithName("GetColorTheme")
.WithOpenApi();

app.MapPost($"{basePath}getbackgrounddescription", async (CopyAndColors copyandcolors, OpenAIClient openai) =>
{
    string? deploymentName = app.Configuration.GetValue<String>("AZURE_OPENAI_CHATGPT_DEPLOYMENT");
    if (deploymentName == null)
    {
        throw new InvalidOperationException("OpenAI Endpoint not configured");
    }
    string instructionsPrompt = @"You are an AI Assistant that creates short, simple image descriptions for AI image generation. 
You will be provided a list of colors, provide the description of a simple background using opposite colors. Provide no explanation as to why choices were made.";
//You will be provided marketing copy and a list of colors. Your job is to provide a description of a gradient background 
//that uses complementary colors and fits the emotion of the copy to be used for diffusion based generation. Provide only the description of the background.";

    string prompt = "Colors: " + copyandcolors.Colors;
    var PromptMessages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, instructionsPrompt),
        new ChatMessage(ChatRole.User, prompt)
    };
    var ChatOptions = new ChatCompletionsOptions(messages: PromptMessages, deploymentName: deploymentName)
    {
        Temperature = (float?)0.4
    };
    ChatCompletions completions = await openai.GetChatCompletionsAsync(ChatOptions);
   
    return new { Description = completions.Choices[0].Message.Content };
})
.WithName("GetBackgroundDescription")
.WithOpenApi();

app.MapPost($"{basePath}generatebackgrounds", async (BackgroundDescription description, OpenAIClient openai) =>
{
    var ImageGenOptions = new ImageGenerationOptions()
    {
        ImageCount = 4,
        Prompt = "Simple image background: " + description.Description
    };
    ImageGenerations images = await openai.GetImageGenerationsAsync(ImageGenOptions);

    List<Uri> imageUrls = images.Data.Select(image => image.Url).ToList();
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
    switch(info.PostType)
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
    var PromptMessages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, instructionsPrompt),
        new ChatMessage(ChatRole.User, info.Copy)
    };
    var ChatOptions = new ChatCompletionsOptions(messages: PromptMessages, deploymentName: deploymentName)
    {
        Temperature = (float?)0.7
    };
    ChatCompletions completions = await openai.GetChatCompletionsAsync(ChatOptions);
    return new { Copy = completions.Choices[0].Message.Content.ReplaceLineEndings("<br />") };

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

            sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddDays(1);
            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write | BlobSasPermissions.Create);

            return new { SasUri = blobClient.GenerateSasUri(sasBuilder) };
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

internal record CopyAndColors(string Copy, string Colors);

internal record BackgroundDescription(string Description);

internal record SocialImage(Uri ForegroundImage, List<Uri> BackgroundImages);
