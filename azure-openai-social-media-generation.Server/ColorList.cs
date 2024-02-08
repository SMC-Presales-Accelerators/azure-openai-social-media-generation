using System.Collections;
using System.Collections.Concurrent;
using System.Numerics;
namespace azure_openai_social_media_generation.Server
{
    public class ColorList
    {
        public List<ColorVector> Colors { get; set; }

        public ColorList()
        {
            Colors = new List<ColorVector>
            {
                new ColorVector("Red",
                    new List<Vector3> {
                        new Vector3(255, 235, 238),
                        new Vector3(255, 205, 210),
                        new Vector3(239, 154, 154),
                        new Vector3(229, 115, 115),
                        new Vector3(239, 83, 80),
                        new Vector3(244, 67, 54),
                        new Vector3(229, 57, 53),
                        new Vector3(211, 47, 47),
                        new Vector3(198, 40, 40),
                        new Vector3(183, 28, 28),
                    }
                ),
                new ColorVector("Pink",
                    new List<Vector3> {
                        new Vector3(252, 228, 236),
                        new Vector3(248, 187, 208),
                        new Vector3(244, 143, 177),
                        new Vector3(240, 98, 146),
                        new Vector3(236, 64, 122),
                        new Vector3(233, 30, 99),
                        new Vector3(216, 27, 96),
                        new Vector3(194, 24, 91),
                        new Vector3(173, 20, 87),
                        new Vector3(136, 14, 79),

                    }
                ),
                new ColorVector("Purple",
                    new List<Vector3> {
                        new Vector3(243, 229, 245),
                        new Vector3(225, 190, 231),
                        new Vector3(206, 147, 216),
                        new Vector3(186, 104, 200),
                        new Vector3(171, 71, 188),
                        new Vector3(156, 39, 176),
                        new Vector3(142, 36, 170),
                        new Vector3(123, 31, 162),
                        new Vector3(106, 27, 154),
                        new Vector3(74, 20, 140),

                    }
                ),
                new ColorVector("Deep Purple",
                    new List<Vector3> {
                        new Vector3(237, 231, 246),
                        new Vector3(209, 196, 233),
                        new Vector3(179, 157, 219),
                        new Vector3(149, 117, 205),
                        new Vector3(126, 87, 194),
                        new Vector3(103, 58, 183),
                        new Vector3(94, 53, 177),
                        new Vector3(81, 45, 168),
                        new Vector3(69, 39, 160),
                        new Vector3(49, 27, 146),

                    }
                ),
                new ColorVector("Indigo",
                    new List<Vector3> {
                        new Vector3(232, 234, 246),
                        new Vector3(197, 202, 233),
                        new Vector3(159, 168, 218),
                        new Vector3(121, 134, 203),
                        new Vector3(92, 107, 192),
                        new Vector3(63, 81, 181),
                        new Vector3(57, 73, 171),
                        new Vector3(48, 63, 159),
                        new Vector3(40, 53, 147),
                        new Vector3(26, 35, 126),

                    }
                ),
                new ColorVector("Blue",
                    new List<Vector3> {
                        new Vector3(227, 242, 253),
                        new Vector3(187, 222, 251),
                        new Vector3(144, 202, 249),
                        new Vector3(100, 181, 246),
                        new Vector3(66, 165, 245),
                        new Vector3(33, 150, 243),
                        new Vector3(30, 136, 229),
                        new Vector3(25, 118, 210),
                        new Vector3(21, 101, 192),
                        new Vector3(13, 71, 161),

                    }
                ),
                new ColorVector("Light Blue",
                    new List<Vector3> {
                        new Vector3(225, 245, 254),
                        new Vector3(179, 229, 252),
                        new Vector3(129, 212, 250),
                        new Vector3(79, 195, 247),
                        new Vector3(41, 182, 246),
                        new Vector3(3, 169, 244),
                        new Vector3(3, 155, 229),
                        new Vector3(2, 136, 209),
                        new Vector3(2, 119, 189),
                        new Vector3(1, 87, 155),

                    }
                ),
                new ColorVector("Cyan",
                    new List<Vector3> {
                        new Vector3(224, 247, 250),
                        new Vector3(178, 235, 242),
                        new Vector3(128, 222, 234),
                        new Vector3(77, 208, 225),
                        new Vector3(38, 198, 218),
                        new Vector3(0, 188, 212),
                        new Vector3(0, 172, 193),
                        new Vector3(0, 151, 167),
                        new Vector3(0, 131, 143),
                        new Vector3(0, 96, 100),

                    }
                ),
                new ColorVector("Teal",
                    new List<Vector3> {
                        new Vector3(224, 242, 241),
                        new Vector3(178, 223, 219),
                        new Vector3(128, 203, 196),
                        new Vector3(77, 182, 172),
                        new Vector3(38, 166, 154),
                        new Vector3(0, 150, 136),
                        new Vector3(0, 137, 123),
                        new Vector3(0, 121, 107),
                        new Vector3(0, 105, 92),
                        new Vector3(0, 77, 64),

                    }
                ),
                new ColorVector("Green",
                    new List<Vector3> {
                        new Vector3(232, 245, 233),
                        new Vector3(200, 230, 201),
                        new Vector3(165, 214, 167),
                        new Vector3(129, 199, 132),
                        new Vector3(102, 187, 106),
                        new Vector3(76, 175, 80),
                        new Vector3(67, 160, 71),
                        new Vector3(56, 142, 60),
                        new Vector3(46, 125, 50),
                        new Vector3(27, 94, 32),

                    }
                ),
                new ColorVector("Light Green",
                    new List<Vector3> {
                        new Vector3(241, 248, 233),
                        new Vector3(220, 237, 200),
                        new Vector3(197, 225, 165),
                        new Vector3(174, 213, 129),
                        new Vector3(156, 204, 101),
                        new Vector3(139, 195, 74),
                        new Vector3(124, 179, 66),
                        new Vector3(104, 159, 56),
                        new Vector3(85, 139, 47),
                        new Vector3(51, 105, 30),
                    }
                ),
                new ColorVector("Lime",
                    new List<Vector3> {
                        new Vector3(249, 251, 231),
                        new Vector3(240, 244, 195),
                        new Vector3(230, 238, 156),
                        new Vector3(220, 231, 117),
                        new Vector3(212, 225, 87),
                        new Vector3(205, 220, 57),
                        new Vector3(192, 202, 51),
                        new Vector3(175, 180, 43),
                        new Vector3(158, 157, 36),
                        new Vector3(130, 119, 23),

                    }
                ),
                new ColorVector("Yellow",
                    new List<Vector3> {
                        new Vector3(255, 253, 231),
                        new Vector3(255, 249, 196),
                        new Vector3(255, 245, 157),
                        new Vector3(255, 241, 118),
                        new Vector3(255, 238, 88),
                        new Vector3(255, 235, 59),
                        new Vector3(253, 216, 53),
                        new Vector3(251, 192, 45),
                        new Vector3(249, 168, 37),
                        new Vector3(245, 127, 23),

                    }
                ),
                new ColorVector("Amber",
                    new List<Vector3> {
                        new Vector3(255, 248, 225),
                        new Vector3(255, 236, 179),
                        new Vector3(255, 224, 130),
                        new Vector3(255, 213, 79),
                        new Vector3(255, 202, 40),
                        new Vector3(255, 193, 7),
                        new Vector3(255, 179, 0),
                        new Vector3(255, 160, 0),
                        new Vector3(255, 143, 0),
                        new Vector3(255, 111, 0),

                    }
                ),
                new ColorVector("Orange",
                    new List<Vector3> {
                        new Vector3(255, 243, 224),
                        new Vector3(255, 224, 178),
                        new Vector3(255, 204, 128),
                        new Vector3(255, 183, 77),
                        new Vector3(255, 167, 38),
                        new Vector3(255, 152, 0),
                        new Vector3(251, 140, 0),
                        new Vector3(245, 124, 0),
                        new Vector3(239, 108, 0),
                        new Vector3(230, 81, 0),
                    }
                ),
                new ColorVector("Deep Orange",
                    new List<Vector3> {
                        new Vector3(251, 233, 231),
                        new Vector3(255, 204, 188),
                        new Vector3(255, 171, 145),
                        new Vector3(255, 138, 101),
                        new Vector3(255, 112, 67),
                        new Vector3(255, 87, 34),
                        new Vector3(244, 81, 30),
                        new Vector3(230, 74, 25),
                        new Vector3(216, 67, 21),
                        new Vector3(191, 54, 12),
                    }
                ),
            };
        }

        public async Task<IDictionary<string, int>> ColorCounts (List<Vector3> colors)
        {
            var colorCounts = new ConcurrentDictionary<string, int>();
            await Parallel.ForEachAsync(Colors, (colorVector, token) =>
            {
                foreach (var colorVectorColor in colorVector.Colors)
                {
                    foreach (var color in colors)
                    {
                        var distance = Vector3.Distance(color, colorVectorColor);
                        if (distance < 15)
                        {
                            colorCounts.AddOrUpdate(colorVector.Name, 1, (key, oldValue) => oldValue + 1);
                        }
                    }
                }
                return new ValueTask();
            });
            return colorCounts;
        }
    }
}
