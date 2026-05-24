using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SortMyClips
{
    public class SortMyClips : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        
        private SortMyClipsSettingsViewModel settings { get; set; }
        
        private bool _emptyAtGameStart = false;
        private int _filesMovedCount = 0;
        private string[] _mediaExtensions = { "jpg", "jpeg", "png", "bmp", "gif", "mp4", "avi", "mov", "wmv", "flv", "mkv" };

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
            _emptyAtGameStart = IsDirEmpty(settings.Settings.UnsortedPath);
            logger.Info("Screenshot Dir is empty: " + _emptyAtGameStart);
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            logger.Info("Game stopped: " + args.Game.Name);
            logger.Info("Unsorted path: " + settings.Settings.UnsortedPath);
            logger.Info("File Moved Count set: " + settings.Settings.ScreenshotsMovedCount);

            if (_emptyAtGameStart)
            {
                logger.Info("Directory was empty at game start, checking for new files...");

                if (!(IsDirEmpty(settings.Settings.UnsortedPath)))
                {
                    logger.Info("Is directory empty: " + IsDirEmpty(settings.Settings.UnsortedPath));

                    string gameName = ReplaceInvalidChars(args.Game.Name);
                    logger.Info("Sanitized game name: " + gameName);

                    string screenDir;
                    if (settings.Settings.UnsortedPath.EndsWith("\\"))
                    { 
                        screenDir = settings.Settings.UnsortedPath + gameName + "\\";
                        logger.Info("Screen directory path: " + screenDir);
                    } else { 
                        screenDir = settings.Settings.UnsortedPath + "\\" + gameName + "\\";
                        logger.Info("Screen directory path: " + screenDir);
                    }

                    if (!Directory.Exists(screenDir))
                    {
                        Directory.CreateDirectory(screenDir);
                        logger.Info("Created screen directory: " + screenDir);
                    }
                    else
                    {
                        logger.Info("Screen directory already exists: " + screenDir);
                    }

                    string[] files = Directory.GetFiles(settings.Settings.UnsortedPath);
                    bool MoveAllFiles = false;

                    var yes = new MessageBoxOption("Yes", false, false);
                    var no = new MessageBoxOption("No", true, false);
                    var yesForAll = new MessageBoxOption("Yes (for all)", false, false);
                    var noForAll = new MessageBoxOption("No (for all)", false, false);
                    var options = new List<MessageBoxOption> {};
                    options.Add(yes);
                    options.Add(no);
                    options.Add(yesForAll);
                    options.Add(noForAll);

                    foreach (string file in files)
                    {
                        logger.Info("Found file: " + file);
                        logger.Info("destination path: " + screenDir + "[" + gameName + "]" + File.GetCreationTime(file));
                        
                        string creationTime = File.GetCreationTime(file).ToString("yyyy-MM-dd_HH-mm-ss");
                        string type = Path.GetExtension(file);
                        string fileName = "[" + gameName + "] " + creationTime + "." + type;
                        
                        if (!_mediaExtensions.Contains(type.ToLower()))
                        {
                            MessageBoxOption illegalFilesPrompt = null;
                            logger.Info("Potential Non-Media File found: " + file);

                            if (!MoveAllFiles)
                            {
                                illegalFilesPrompt = PlayniteApi.Dialogs.ShowMessage(
                                    "Potential Non-Media File found: " + file +
                                    "\nAre you sure you want to move this file?", "",
                                    MessageBoxImage.Warning, options);
                            }

                            if (!MoveAllFiles)
                            {
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
                                    MoveAllFiles = true;
                                }
                            }

                        }
                        
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
                }
                else
                {
                    logger.Info("Is directory empty: " + IsDirEmpty(settings.Settings.UnsortedPath));
                }
                if (_filesMovedCount > 0 && settings.Settings.ScreenshotsMovedCount)
                {
                    NotificationMessage msg = new NotificationMessage("SortMyClips", "[Screenshot & Clips Organizer]\nMoved " + _filesMovedCount + " file(s to " + args.Game.Name + " folder", NotificationType.Info);
                    PlayniteApi.Notifications.Add(msg);
                }

                _filesMovedCount = 0;
            }
            else
            {
                logger.Info("Directory was not empty at game start, skipping...");
                NotificationMessage msg = new NotificationMessage("SortMyClips", "[Screenshot & Clips Organizer]\nDirectory was not empty at game start", NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (settings.Settings.UnsortedPath == string.Empty)
            {
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nScreenshot Directory is not set", NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }
            else if (!IsDirEmpty(settings.Settings.UnsortedPath))
            {
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nScreenshot Directory is not empty", NotificationType.Error);
                PlayniteApi.Notifications.Add(msg);
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (!IsDirEmpty(settings.Settings.UnsortedPath))
            {
                NotificationMessage msg = new NotificationMessage("SortMyClips",
                    "[Screenshot & Clips Organizer]\nScreenshot Directory is not empty", NotificationType.Error);
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