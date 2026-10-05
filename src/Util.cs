using System;
using System.IO;

namespace SortMyClips
{
    public static class Util
    {
        /// <summary>
        /// Replaces invalid characters in a filename with an empty string.
        /// </summary>
        /// <param name="filename">The filename to process.</param>
        /// <returns>The filename with invalid characters removed.</returns>
        public static string ReplaceInvalidChars(string filename)
        {
            var sanitized = string.Concat(filename.Split(Path.GetInvalidFileNameChars()))
                .TrimEnd(' ', '.');

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return "_";
            }

            var deviceName = sanitized.Split('.')[0];
            if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                (deviceName.Length == 4 &&
                 (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                  deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                 deviceName[3] >= '1' && deviceName[3] <= '9'))
            {
                return "_" + sanitized;
            }

            return sanitized;
        }

    }
}