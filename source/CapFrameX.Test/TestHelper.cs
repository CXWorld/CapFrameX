using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CapFrameX.Test
{
    public static class TestHelper
    {
        public static List<string> GetAllLinesFromRessourceFile(string filename)
        {
            var thisAssembly = Assembly.GetExecutingAssembly();
            string resourceName = GetRessourceName(filename);

            var lines = new List<string>();
            using (var stream = thisAssembly.GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }

            return lines;
        }

        public static string GetRessourceName(string filename)
        {
            var thisAssembly = Assembly.GetExecutingAssembly();
            return thisAssembly.GetManifestResourceNames()
                 .Single(str => str.EndsWith(filename));
        }
    }
}
