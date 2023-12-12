using Azure.AI.OpenAI;
namespace azure_openai_social_media_generation.Server
{
    public class SocialMediaResponse
    {
        public string Post {  get; set; }
        public string ImageDescription { get; set; }
        public List<Uri> ImageUrls { get; set; }

        public SocialMediaResponse(string post, string image_description) { 
            Post = post;
            ImageDescription = image_description;
            ImageUrls = new List<Uri>();
        }
    }

}
