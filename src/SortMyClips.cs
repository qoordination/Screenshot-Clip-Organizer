using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace SortMyClips
{
    public class SortMyClips : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SortMyClipsSettingsViewModel settings { get; set; }

        private FileManager fileManager { get; set; }

        private bool _setupFinished;

        public override Guid Id { get; } = Guid.Parse("dfef3e4e-365c-474f-9a9c-5eaaadbc1d59");

        public SortMyClips(IPlayniteAPI api) : base(api)
        {
            fileManager = new FileManager();
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
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            _setupFinished = fileManager.CaptureInitialFileState(settings.Settings.UnsortedPaths);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (!_setupFinished)
            {
                logger.Error("Setup was not finished.");
                return;
            }
            _setupFinished = false;
            
            if (!CheckPathsConfigured())
            {
                logger.Info("Paths are not configured correctly.");
                return;
            }
            
            var newFilesAmount = fileManager.FindNewFiles(settings.Settings.UnsortedPaths);
            logger.Info("File Moved Count setting: " + settings.Settings.DisplayAmountMoved);

            if ((settings.Settings.SortedPath != string.Empty) && newFilesAmount != 0)
            {
                // Sanitize game name to remove invalid characters for file paths
                var gameName = Util.ReplaceInvalidChars(args.Game.Name);
                logger.Info("Sanitized game name: " + gameName);

                var screenDir = Path.Combine(settings.Settings.SortedPath, gameName);
                logger.Info("Screen directory path: " + screenDir);

                FileManager.SetupFolder(gameName, screenDir, args.Game.GameId, settings.Settings.SortedPath, GetPluginUserDataPath());

                HandleIllegalFiles();

                HandleFileMoving(screenDir, gameName);
            }
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            CheckPathsConfigured();

            CreateDataJson();
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            CheckPathsConfigured();

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

        private void HandleIllegalFiles()
        {
            var illegalFiles = fileManager.FindIllegalFiles(settings.Settings.MediaExtensions);
            var options = GetIllegalFileOptions();
            foreach (var file in illegalFiles)
            {
                var result = PlayniteApi.Dialogs.ShowMessage(
                    $"The file '{Path.GetFileName(file)}' is not a recognized media file. Do you want to move it?",
                    "Illegal File Detected",
                    MessageBoxImage.Warning,
                    options);

                if (result == options[1]) // No
                {
                    fileManager.RemoveNewFile(file);
                }
                else if (result == options[2]) // Yes (for all following)
                {
                    // Move all remaining illegal files
                    break;
                }
                else if (result == options[3]) // No (for all following)
                {
                    // Remove all remaining illegal files from the list
                    foreach (var remainingFile in illegalFiles.Skip(illegalFiles.IndexOf(file)))
                    {
                        fileManager.RemoveNewFile(remainingFile);
                    }

                    break;
                }
            }
        }

        private void HandleFileMoving(string screenDir, string gameName)
        {
            var filesMovedCount = fileManager.MoveFiles(screenDir, gameName,
                settings.Settings.FileModeCopy);

            // If wished, display notification of how many files were moved, with option to open folder
            if (filesMovedCount <= 0 || !settings.Settings.DisplayAmountMoved) return;
            var operationType = (settings.Settings.FileModeCopy) ? "Copied" : "Moved";
            void OpenFolderAction() => Process.Start("explorer.exe", screenDir);
            NotificationMessage msg = new NotificationMessage("SortMyClips",
                "[Screenshot & Clips Organizer]\n" + operationType + " " + filesMovedCount + " file(s) to " + gameName +
                " folder\nClick to open", NotificationType.Info, OpenFolderAction);
            PlayniteApi.Notifications.Add(msg);
        }

        private static List<MessageBoxOption> GetIllegalFileOptions()
        {
            var yes = new MessageBoxOption("Yes");
            var no = new MessageBoxOption("No", true);
            var yesForAll = new MessageBoxOption("Yes (for all following)");
            var noForAll = new MessageBoxOption("No (for all following)");
            return new List<MessageBoxOption>
            {
                yes,
                no,
                yesForAll,
                noForAll
            };
        }

        private void CreateDataJson()
        {
            // Keep existing names so SetupFolder can rename folders after a library name change.
            var dataJsonPath = Path.Combine(GetPluginUserDataPath(), "data.json");
            var mappings = FileManager.LoadGameMappings(dataJsonPath);

            foreach (var game in PlayniteApi.Database.Games)
            {
                if (!mappings.ContainsKey(game.GameId))
                {
                    mappings.Add(game.GameId, Util.ReplaceInvalidChars(game.Name));
                }
            }

            // Always rewrite the file so legacy newline-delimited data is migrated
            // and malformed/partial files are repaired.
            FileManager.SaveGameMappings(dataJsonPath, mappings);
            logger.Info("Data Json updated (" + mappings.Count + " items)");
        }
        
        private bool CheckPathsConfigured()
        {
            if (settings.Settings.UnsortedPaths.Count == 0)
            {
                logger.Error("No screenshot directory set.");
                var msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nNo screenshot directory is set", NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }
            if (settings.Settings.SortedPath == string.Empty)
            {
                logger.Error("Sorted directory not set.");
                var msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nSorting target directory is not set.",
                    NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }
            return !(settings.Settings.UnsortedPaths.Count == 0 || settings.Settings.SortedPath == string.Empty);
        }

        // Main menu manual refresh menu option and option to open sorted media folder
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs menuArgs)
        {
            // Option for manual refresh of data JSON
            yield return new MainMenuItem
            {
                Description = "Manually refresh Screenshot & Clips Organizer",
                MenuSection = "@Screenshot & Clips Organizer",
                Action = args =>
                {
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