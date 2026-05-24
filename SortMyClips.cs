using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace SortMyClips
{
    public class SortMyClips : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SortMyClipsSettingsViewModel settings { get; set; }

        private int _filesMovedCount;
        private bool _otherFolderFound;

        private string[] _mediaExtensions =
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm" };

        private string[] _initialDirState;

        public override Guid Id { get; } = Guid.Parse("dfef3e4e-365c-474f-9a9c-5eaaadbc1d59");

        public SortMyClips(IPlayniteAPI api) : base(api)
        {
            settings = new SortMyClipsSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            _initialDirState = Directory.GetFiles(settings.Settings.UnsortedPath);
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            logger.Info("Game stopped: " + args.Game.Name);
            logger.Info("Unsorted path: " + settings.Settings.UnsortedPath);
            logger.Info("File Moved Count setting: " + settings.Settings.ScreenshotsMovedCount);

            logger.Info("Is directory empty: " + IsDirEmpty(settings.Settings.UnsortedPath));

            // Sanitize game name to remove invalid characters for file paths
            string gameName = ReplaceInvalidChars(args.Game.Name);
            logger.Info("Sanitized game name: " + gameName);

            string screenDir = settings.Settings.UnsortedPath + gameName + "\\";
            logger.Info("Screen directory path: " + screenDir);

            string[] newDirState = Directory.GetFiles(settings.Settings.UnsortedPath);
            string[] newFiles = newDirState.Except(_initialDirState).ToArray();

            // If no new files are found, exit method
            if (newFiles.Length == 0)
            {
                logger.Info("No new files found in unsorted directory.");
                return;
            }

            if (!Directory.Exists(screenDir))
            {
                // Check if gameId is in JSON and match the JSON game name with the current game name. Rename folder and update JSON, if not matching.
                string[] jsonContent = File.ReadAllLines(GetPluginUserDataPath() + "\\data.json");
                for (int i = 0; i < jsonContent.Length; i++)
                {
                    var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent[i]);
                    if (obj.ContainsKey(args.Game.GameId) && obj[args.Game.GameId] != gameName &&
                        Directory.Exists(Path.Combine(settings.Settings.UnsortedPath, obj[args.Game.GameId])))
                    {
                        _otherFolderFound = true;
                        Directory.Move(settings.Settings.UnsortedPath + obj[args.Game.GameId], screenDir);
                        logger.Info("Renamed existing game folder from " + obj[args.Game.GameId] + " to " + gameName);
                        obj[args.Game.GameId] = gameName;
                        jsonContent[i] = JsonSerializer.Serialize(obj);
                        File.WriteAllLines(GetPluginUserDataPath() + "\\data.json", jsonContent);
                        break;
                    }
                }

                // If the game doesn't have an entry in the JSON or if the folder name in the JSON matches the current game name, create a new entry and folder
                if (!(_otherFolderFound))
                {
                    var data = new Dictionary<string, string>();
                    data.Add(args.Game.GameId, gameName);
                    string jsonString =
                        JsonSerializer.Serialize(data);
                    File.AppendAllText(GetPluginUserDataPath() + "\\data.json", jsonString + "\n");
                    logger.Info("Wrote game id to data json: " + gameName);

                    Directory.CreateDirectory(screenDir);
                    logger.Info("Created screen directory: " + screenDir);
                }
            }
            else
            {
                logger.Info("Screen directory already exists: " + screenDir);
            }

            // Set up message box options for handling non-media files
            bool moveAllFiles = false;
            var yes = new MessageBoxOption("Yes", false, false);
            var no = new MessageBoxOption("No", true, false);
            var yesForAll = new MessageBoxOption("Yes (for all)", false, false);
            var noForAll = new MessageBoxOption("No (for all)", false, false);
            var options = new List<MessageBoxOption> { };
            options.Add(yes);
            options.Add(no);
            options.Add(yesForAll);
            options.Add(noForAll);

            foreach (string file in newFiles)
            {
                logger.Info("Found file: " + file);
                logger.Info(
                    "destination path: " + screenDir + "[" + gameName + "]" + File.GetCreationTime(file));

                // Generate new file name based on game and creation time, keep original file extension
                string creationTime = File.GetCreationTime(file).ToString("yyyy-MM-dd_HH-mm-ss");
                string type = Path.GetExtension(file);
                string fileName = "[" + gameName + "] " + creationTime + type;

                //Potentially illegal file found 
                if (!(_mediaExtensions.Contains(type)))
                {
                    if (!moveAllFiles)
                    {
                        MessageBoxOption illegalFilesPrompt;
                        logger.Info("Potential Non-Media File found: " + file);

                        illegalFilesPrompt = PlayniteApi.Dialogs.ShowMessage(
                            "Potential Non-Media File found: " + file +
                            "\nAre you sure you want to move this file?", "",
                            MessageBoxImage.Warning, options);

                        if (illegalFilesPrompt == yes)
                        {
                            logger.Info("User chose to move this file: " + file);
                        }
                        else if (illegalFilesPrompt == no)
                        {
                            logger.Info("User chose to skip this file: " + file);
                            continue;
                        }

                        else if (illegalFilesPrompt == noForAll)
                        {
                            logger.Info("User chose to skip all non-media files.");
                            break;
                        }
                        else if (illegalFilesPrompt == yesForAll)
                        {
                            logger.Info("User chose to move all files, including non-media files.");
                            moveAllFiles = true;
                        }
                    }
                }

                // Move if file doesn't already exist at destination
                if (!(File.Exists(screenDir + fileName)))
                {
                    File.Move(file, screenDir + fileName);
                    logger.Info("Moved and renamed " + file + " to " + screenDir);
                    _filesMovedCount++;
                }
                else
                {
                    logger.Info("File already exists at destination: " + screenDir + fileName);
                }
            }

            // If wished, display notification of how many files were moved, with option to open folder
            if (_filesMovedCount > 0 && settings.Settings.ScreenshotsMovedCount)
            {
                Action openFolderAction = () => Process.Start("explorer.exe", screenDir);
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nMoved " + _filesMovedCount + " file(s) to " + gameName +
                    " folder\nClick to open", NotificationType.Info, openFolderAction);
                PlayniteApi.Notifications.Add(msg);
            }

            _filesMovedCount = 0;
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (settings.Settings.UnsortedPath == string.Empty)
            {
                logger.Info("Screenshot directory not set.");
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nScreenshot Directory is not set", NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }

            // Check if data JSON exists, if not create and add all games. Used for folder naming and renaming purposes
            logger.Info("Data folder: " + GetPluginUserDataPath());
            logger.Info("Does data json exist:" + File.Exists(GetPluginUserDataPath() + "\\data.json"));
            if (!File.Exists(GetPluginUserDataPath() + "\\data.json"))
            {
                logger.Info("Creating data json file.");
                
                var sb = new StringBuilder();
                int count = 0;
                foreach (var game in PlayniteApi.Database.Games)
                {
                    count++;
                    logger.Info("Id: " + game.GameId + " Name: " + ReplaceInvalidChars(game.Name));
                    var data = new Dictionary<string, string>();
                    data.Add(game.GameId, ReplaceInvalidChars(game.Name));
                    string jsonString =
                        JsonSerializer.Serialize(data);
                    sb.AppendLine(jsonString);
                }

                if (sb.Length > 0)
                {
                    string dataJsonPath = Path.Combine(GetPluginUserDataPath(), "data.json");
                    logger.Info(dataJsonPath);
                    try
                    {
                        File.AppendAllText(dataJsonPath, sb.ToString());
                        logger.Info("Data Json done (" + count + " items)");
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Error during data json");
                    }
                }
                else
                {
                    logger.Info("No games found in database to write to data json.");
                }
                logger.Info("Got here!");
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (settings.Settings.UnsortedPath == string.Empty)
            {
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nScreenshot Directory is not set", NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SortMyClipsSettingsView();
        }

        public bool IsDirEmpty(string dir)
        {
            string[] files = Directory.GetFiles(dir);
            return (files.Length == 0);
        }

        public string ReplaceInvalidChars(string filename)
        {
            return string.Join(" ", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}