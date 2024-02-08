using System.Numerics;

namespace azure_openai_social_media_generation.Server
{
    public class ColorVector
    {
        public string Name { get; set; }
        public List<Vector3> Colors { get; set; }

        public ColorVector(string name, List<Vector3> colors)
        {
            Name = name;
            Colors = colors;
        }
    }
}
