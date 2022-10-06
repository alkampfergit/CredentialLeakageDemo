using Serilog;

namespace OhMyDemoConsole.Library
{
    public static class ConfigurationHelper
    {
        public static string? FindOverrideParentConfig(string fileName)
        {
            var directoryToCheck = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent;
            Log.Debug("Starting to look override file {0} starting from directory {1}", fileName, AppDomain.CurrentDomain.BaseDirectory);
            while (directoryToCheck != null)
            {
                string overrideFile = Path.Combine(directoryToCheck.FullName, fileName);
                if (File.Exists(overrideFile))
                {
                    Log.Debug("Found override configuration file: {0}", overrideFile);
                    return overrideFile;
                }
                directoryToCheck = directoryToCheck.Parent;
            }

            Log.Debug("No override configuration file {0} found starting from directory {1}", fileName, AppDomain.CurrentDomain.BaseDirectory);
            return null;
        }
    }
}
