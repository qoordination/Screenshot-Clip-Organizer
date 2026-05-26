using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace SortMyClips
{
    public class SortMyClips : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SortMyClipsSettingsViewModel settings { get; set; }

        private int _filesMovedCount;
        private bool _otherFolderFound;
        private bool _keyExists;

        private string[] _mediaExtensions =
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm" };

        private string[] _initialDirState = {string.Empty};

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
            logger.Info("Unsorted Directory Paths:" + settings.Settings.UnsortedPath.Length);
            foreach (string unsortedPath in settings.Settings.UnsortedPath)
            {
                try
                {
                    _initialDirState = _initialDirState
                        .Concat(Directory.GetFiles(unsortedPath, "*.*", SearchOption.AllDirectories)).ToArray();
                }
                catch (Exception e)
                {
                    logger.Info("Error accessing unsorted directory: " + unsortedPath + "\n" + e);
                }
            }
            logger.Info("New state: "  + _initialDirState.Length);
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            string[] newDirState = {string.Empty};
            foreach (string unsortedPath in settings.Settings.UnsortedPath)
            {
                logger.Info("Game stopped: " + args.Game.Name);
                logger.Info("Unsorted path: " + unsortedPath);
                logger.Info("File Moved Count setting: " + settings.Settings.ScreenshotsMovedCount);

                if ((unsortedPath != string.Empty && settings.Settings.SortedPath != string.Empty))
                {
                    // Sanitize game name to remove invalid characters for file paths
                    string gameName = ReplaceInvalidChars(args.Game.Name);
                    logger.Info("Sanitized game name: " + gameName);

                    string screenDir = Path.Combine(settings.Settings.SortedPath, gameName) + "\\";
                    logger.Info("Screen directory path: " + screenDir);

                    // Get list of new files in unsorted dir, compared to initial game start state
                    newDirState = newDirState.Concat(Directory.GetFiles(unsortedPath, "*.*", SearchOption.AllDirectories)).ToArray();
                    string[] newFiles = newDirState.Except(_initialDirState).ToArray();

                    // If no new files are found, exit method
                    if (newFiles.Length == 0)
                    {
                        logger.Info("No new files found in unsorted directory.");
                        continue;
                    }

                    SetupFolder(gameName, screenDir, args);

                    MoveFiles(screenDir, newFiles, gameName);
                }
                else if (unsortedPath == string.Empty)
                {
                    logger.Info("Screenshot directory not set.");
                    NotificationMessage msg = new NotificationMessage("SortMyClips",
                        "[Screenshot & Clips Organizer]\nUnsorted source directory is not set, could not move media.",
                        NotificationType.Error);
                    PlayniteApi.Notifications.Add(msg);
                }
                else if (settings.Settings.SortedPath == string.Empty)
                {
                    logger.Info("Sorted directory not set.");
                    NotificationMessage msg = new NotificationMessage("SortMyClips",
                        "[Screenshot & Clips Organizer]\nSorting target directory is not set, could not move media.",
                        NotificationType.Error);
                    PlayniteApi.Notifications.Add(msg);
                }
            }
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (settings.Settings.UnsortedPath == Array.Empty<string>())
            {
                logger.Info("No screenshot directory set.");
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nNo screenshot directory is set", NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            } else if (settings.Settings.SortedPath == string.Empty)
            {
                logger.Info("Sorted directory not set.");
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nSorting target directory is not set.",
                    NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }

            CreateDataJson();
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (settings.Settings.UnsortedPath == Array.Empty<string>())
            {
                logger.Info("No screenshot directory set.");
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nNo screenshot directory is set", NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            } else if (settings.Settings.SortedPath == string.Empty)
            {
                logger.Info("Sorted directory not set.");
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nSorting target directory is not set.",
                    NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }
            CreateDataJson();
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SortMyClipsSettingsView();
        }

        public string ReplaceInvalidChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        public void MoveFiles (string screenDir, string[] newFiles, string gameName)
        {
                // Set up message box options for handling non-media files
                bool moveAllFiles = false;
                var yes = new MessageBoxOption("Yes", false, false);
                var no = new MessageBoxOption("No", true, false);
                var yesForAll = new MessageBoxOption("Yes (for all following)", false, false);
                var noForAll = new MessageBoxOption("No (for all following)", false, false);
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
                        if (settings.Settings.FileModeCopy)
                        {
                            File.Copy(file, screenDir + fileName);
                            logger.Info("Copied and renamed " + file + " to " + screenDir + fileName);
                        }
                        else
                        {
                            File.Move(file, screenDir + fileName);
                            logger.Info("Moved and renamed " + file + " to " + screenDir + fileName);
                            _filesMovedCount++;
                        }
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
            
        // Check if folder for game already exists, if not create it. If folder with same game id but different name exists, rename it to match current game name and update JSON
        public void SetupFolder(string gameName, string screenDir, OnGameStoppedEventArgs args)
        {
                if (!Directory.Exists(screenDir))
                {
                    string[] jsonContent = File.ReadAllLines(GetPluginUserDataPath() + "\\data.json");
                    for (int i = 0; i < jsonContent.Length; i++)
                    {
                        Dictionary<string, string> obj;
                        try
                        {
                            obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent[i]);
                        }
                        catch (Exception e)
                        {
                            logger.Error("Error deserializing JSON content: " + e);
                            continue;
                        }

                        if (obj.ContainsKey(args.Game.GameId))
                        {
                            _keyExists = true;
                            logger.Info("Found game id in data json: " + args.Game.GameId);
                        }

                        if (obj.ContainsKey(args.Game.GameId) && obj[args.Game.GameId] != gameName &&
                            Directory.Exists(Path.Combine(settings.Settings.SortedPath, obj[args.Game.GameId])))
                        {
                            _otherFolderFound = true;
                            Directory.Move(Path.Combine(settings.Settings.SortedPath, obj[args.Game.GameId]),
                                screenDir);
                            logger.Info(
                                "Renamed existing game folder from " + obj[args.Game.GameId] + " to " + gameName);
                            obj[args.Game.GameId] = gameName;
                            jsonContent[i] = JsonConvert.SerializeObject(obj);
                            File.WriteAllLines(GetPluginUserDataPath() + "\\data.json", jsonContent);
                            break;
                        }
                    }

                    logger.Info("Key exists in JSON: " + _keyExists);
                    logger.Info("Other folder with same game id found in JSON: " + _otherFolderFound);
                    // If the game doesn't have an entry in the JSON or if the folder name in the JSON matches the current game name, create a new entry and folder
                    if (!(_otherFolderFound))
                    {
                        var data = new Dictionary<string, string>();
                        data.Add(args.Game.GameId, gameName);
                        string jsonString =
                            JsonConvert.SerializeObject(data);
                        if (!(_keyExists))
                        {
                            File.AppendAllText(GetPluginUserDataPath() + "\\data.json", jsonString + "\n");
                            logger.Info("Wrote game id to data json: " + gameName);
                        }

                        Directory.CreateDirectory(screenDir);
                        logger.Info("Created screen directory: " + screenDir);
                    }
                }
                else
                {
                    logger.Info("Screen directory already exists: " + screenDir);
                }
        }

        public void CreateDataJson()
        {
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
                    var data = new Dictionary<string, string>();
                    data.Add(game.GameId, ReplaceInvalidChars(game.Name));
                    string jsonString;
                    try
                    {
                        jsonString =
                            JsonConvert.SerializeObject(data);
                    }
                    catch (Exception e)
                    {
                        logger.Error("Error serializing JSON content of " + ReplaceInvalidChars(game.Name) + ":\n" + e);
                        continue;
                    }

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
            }
        }

        // Manual refresh menu option
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs menuArgs)
        {
            // Option for manual refresh of data JSON
            yield return new MainMenuItem
            {
                Description = "Manually refresh Screenshot & Clips Organizer",
                MenuSection = "@Screenshot & Clips Organizer",
                Action = args =>
                {
                    File.Delete(GetPluginUserDataPath() + "\\data.json");
                    CreateDataJson();
                    logger.Info("Manually refreshed data json.");
                    NotificationMessage msg = new NotificationMessage("SortMyClips",
                        "[Screenshot & Clips Organizer]\nManually refreshed game data", NotificationType.Info);
                    PlayniteApi.Notifications.Add(msg);
                }
            };
            
            yield return new MainMenuItem
            {
                Description = "Open sorted game media folder",
                MenuSection = "@Screenshot & Clips Organizer",
                Action = args =>
                {
                    if (settings.Settings.SortedPath != string.Empty)
                    {
                        Process.Start("explorer.exe", settings.Settings.SortedPath);
                    }
                    else
                    {
                        logger.Info("Sorted directory not set.");
                        NotificationMessage msg = new NotificationMessage("SortMyClips",
                            "[Screenshot & Clips Organizer]\nSorting target directory is not set, could not open folder.",
                            NotificationType.Error);
                        PlayniteApi.Notifications.Add(msg);
                    }
                }
            };
        }
    }
}