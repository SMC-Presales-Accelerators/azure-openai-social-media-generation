namespace azure_openai_social_media_generation.Server
{
    public class AzureColorResponse
    {
            public Color? color { get; set; }
            public string? requestId { get; set; }
            public Metadata? metadata { get; set; }
    }

    public class Color
    {
        public string? dominantColorForeground { get; set; }
        public string? dominantColorBackground { get; set; }
        public string[]? dominantColors { get; set; }
        public string? accentColor { get; set; }
        public bool isBwImg { get; set; }
        public bool isBWImg { get; set; }
    }

    public class Metadata
    {
        public int height { get; set; }
        public int width { get; set; }
        public string? format { get; set; }
    }
}
