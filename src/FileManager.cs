using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using Playnite.SDK;

namespace SortMyClips
{
    public class FileManager
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly List<string> _initialDirState = new List<string>();
        private readonly List<string> _newFiles = new List<string>();

        /// <summary>
        /// Captures the initial state of the directories specified in the paths list.
        /// </summary>
        /// <param name="paths"> The list of directories to capture the initial state of. </param>
        /// <returns> True if the initial state was captured successfully, false otherwise. </returns>
        public bool CaptureInitialFileState(ObservableCollection<string> paths)
        {
            _initialDirState.Clear();
            if (paths == null || paths.Count == 0)
            {
                logger.Warn("No paths provided to capture initial file state.");
                return false;
            }
            foreach (var path in paths)
            {
                try
                {
                    _initialDirState.AddRange(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error while capturing initial file state in directory: " + path);
                }
            }

            return true;
        }

        /// <summary>
        /// Finds new files in the specified directories that were not present in the initial state.
        /// </summary>
        /// <param name="paths">The list of directories to search for new files.</param>
        /// <returns>The number of new files found.</returns>
        public int FindNewFiles(ObservableCollection<string> paths)
        {
            _newFiles.Clear();
            foreach (var path in paths)
            {
                try
                {
                    var currentFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    foreach (var file in currentFiles)
                    {
                        if (!_initialDirState.Contains(file))
                        {
                            _newFiles.Add(file);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error while searching for new files in directory: " + path);
                }
            }

            return _newFiles.Count;
        }

        /// <summary>
        /// Finds files in the new files list that do not have a valid media extension.
        /// </summary>
        /// <param name="mediaExtensions">List of valid media extensions.</param>
        /// <returns>A list of files with invalid extensions.</returns>
        public List<string> FindIllegalFiles(ObservableCollection<string> mediaExtensions)
        {
            List<string> illegalFiles = new List<string>();
            foreach (var file in _newFiles)
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (!mediaExtensions.Contains(extension))
                {
                    illegalFiles.Add(file);
                }
            }

            return illegalFiles;
        }

        /// <summary>
        /// Removes a file from the new files list.
        /// </summary>
        /// <param name="filePath">The path of the file to remove.</param>
        public void RemoveNewFile(string filePath)
        {
            if (_newFiles.Contains(filePath))
            {
                _newFiles.Remove(filePath);
                logger.Info("Removed file from new files list: " + filePath);
            }
            else
            {
                logger.Warn("Attempted to remove file that was not in new files list: " + filePath);
            }
        }

        /// <summary>
        /// Moves or copies the new files to the specified directory, renaming them based on the game name and creation time.
        /// </summary>
        /// <param name="screenDir">The directory to move or copy the files to.</param>
        /// <param name="gameName">The name of the game.</param>
        /// <param name="copyMode">True to copy the files, false to move them.</param>
        /// <returns>The number of files moved or copied.</returns>
        public int MoveFiles(string screenDir, string gameName, bool copyMode)
        {
            var filesMovedCount = 0;
            foreach (var file in _newFiles)
            {
                logger.Info("Found file: " + file);
                logger.Info(
                    "destination path: " + screenDir + "[" + gameName + "]" + File.GetCreationTime(file));

                // Generate new file name based on game and creation time, keep original file extension
                var creationTime = File.GetCreationTime(file).ToString("yyyy-MM-dd_HH-mm-ss");
                var type = Path.GetExtension(file);
                var fileName = "[" + gameName + "] " + creationTime + type;
                var destinationPath = Path.Combine(screenDir, fileName);

                if (File.Exists(destinationPath))
                {
                    logger.Warn("File already exists at destination: " + destinationPath);
                    while (File.Exists(destinationPath))
                    {
                        // Append a number to the filename to avoid overwriting
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName);
                        var counter = 1;
                        destinationPath = Path.Combine(screenDir, $"{fileNameWithoutExtension} ({counter}){extension}");
                        counter++;
                    }
                }
                
                try
                {
                    if (copyMode)
                    {
                        File.Copy(file, destinationPath);
                        logger.Info("Copied and renamed " + file + " to " + destinationPath);
                    }
                    else
                    {
                        File.Move(file, destinationPath);
                        logger.Info("Moved and renamed " + file + " to " + destinationPath);
                    }

                    filesMovedCount++;
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error while moving/copying file: " + file + " to " + destinationPath);
                }
            }

            return filesMovedCount;
        }

        internal static Dictionary<string, string> LoadGameMappings(string dataPath)
        {
            var mappings = new Dictionary<string, string>();
            if (!File.Exists(dataPath))
            {
                return mappings;
            }

            var content = File.ReadAllText(dataPath);
            if (string.IsNullOrWhiteSpace(content))
            {
                return mappings;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                if (parsed != null)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Migrate the old newline-delimited JSON format below.
            }

            foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var legacyMapping = JsonConvert.DeserializeObject<Dictionary<string, string>>(line);
                    if (legacyMapping != null)
                    {
                        foreach (var mapping in legacyMapping)
                        {
                            mappings[mapping.Key] = mapping.Value;
                        }
                    }
                }
                catch (JsonException e)
                {
                    logger.Error(e, "Error deserializing a data.json entry.");
                }
            }

            return mappings;
        }

        internal static void SaveGameMappings(string dataPath, Dictionary<string, string> mappings)
        {
            var temporaryPath = dataPath + ".tmp";
            var json = JsonConvert.SerializeObject(mappings, Formatting.Indented);
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(dataPath))
            {
                File.Replace(temporaryPath, dataPath, null);
            }
            else
            {
                File.Move(temporaryPath, dataPath);
            }
        }

        /// <summary>
        /// Sets up the folder for the game, preserving and updating its data.json mapping.
        /// </summary>
        /// <param name="gameName">Name of the game</param>
        /// <param name="screenDir">Path to the screen directory</param>
        /// <param name="GameId">ID of the game</param>
        /// <param name="sortedPath">Path to the sorted directory</param>
        /// <param name="userDataPath">Path to the plugin user data folder</param>
        public static void SetupFolder(string gameName, string screenDir, string GameId, string sortedPath, string userDataPath)
        {
            var dataPath = Path.Combine(userDataPath, "data.json");
            var mappings = LoadGameMappings(dataPath);
            var mappingChanged = false;

            if (mappings.TryGetValue(GameId, out var previousGameName))
            {
                if (previousGameName != gameName)
                {
                    var previousFolder = Path.Combine(sortedPath, previousGameName);
                    if (Directory.Exists(previousFolder) && !Directory.Exists(screenDir))
                    {
                        Directory.Move(previousFolder, screenDir);
                        logger.Info(
                            "Renamed existing game folder from " + previousGameName + " to " + gameName);
                    }
                    mappings[GameId] = gameName;
                    mappingChanged = true;
                }
            }
            else
            {
                mappings.Add(GameId, gameName);
                mappingChanged = true;
            }

            if (!Directory.Exists(screenDir))
            {
                Directory.CreateDirectory(screenDir);
                logger.Info("Created screen directory: " + screenDir);
            }

            if (mappingChanged)
            {
                SaveGameMappings(dataPath, mappings);
            }
        }
    }
}