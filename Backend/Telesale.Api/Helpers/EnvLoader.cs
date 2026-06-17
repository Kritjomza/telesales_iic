using System;
using System.IO;

namespace Telesale.Api.Helpers;

public static class EnvLoader
{
    public static void Load(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("#") || trimmedLine.StartsWith("//"))
                continue;

            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = trimmedLine.Substring(0, separatorIndex).Trim();
            var val = trimmedLine.Substring(separatorIndex + 1).Trim();

            // Strip surrounding quotes
            if (val.Length >= 2 && 
                ((val.StartsWith("\"") && val.EndsWith("\"")) || 
                 (val.StartsWith("'") && val.EndsWith("'"))))
            {
                val = val.Substring(1, val.Length - 2);
            }

            if (!string.IsNullOrEmpty(key))
            {
                Environment.SetEnvironmentVariable(key, val);
            }
        }
    }
}
