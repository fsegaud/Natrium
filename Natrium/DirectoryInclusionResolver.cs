using System;
using System.IO;

namespace Natrium
{
    public class DirectoryInclusionResolver : IInclusionResolver
    {
        private readonly string _directory;

        public DirectoryInclusionResolver(string directory)
        {
            _directory = directory;
        }

        public string? ResolveInclusion(string filename)
        {
            try
            {
                if (!filename.Contains('.'))
                    filename = $"{filename}.na";
                string str = File.ReadAllText(Path.Combine(_directory, filename));
                return str;
            }
            catch (IOException)
            {
                return null;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                throw;
            }
        }
    }
}