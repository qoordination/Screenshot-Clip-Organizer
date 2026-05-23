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
using System.Windows.Controls;

namespace SortMyClips
{
    public class SortMyClips : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SortMyClipsSettingsViewModel settings { get; set; }
        
        private bool _emptyAtGameStart = false;

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

            if (_emptyAtGameStart)
            {
                logger.Info("Directory was empty at game start, checking for new files...");

                if (!(IsDirEmpty(settings.Settings.UnsortedPath)))
                {
                    logger.Info("Is directory empty: " + IsDirEmpty(settings.Settings.UnsortedPath));

                    string gameName = ReplaceInvalidChars(args.Game.Name);
                    logger.Info("Sanitized game name: " + gameName);

                    string screenDir = settings.Settings.UnsortedPath + gameName + "\\";
                    logger.Info("Screen directory path: " + screenDir);

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
                    foreach (string s in files)
                    {
                        logger.Info("Found file: " + s);
                        logger.Info("destination path: " + screenDir + "[" + gameName + "]" + File.GetCreationTime(s));
                    }

                    foreach (string file in files)
                    {
                        string creationTime = File.GetCreationTime(file).ToString("yyyy-MM-dd_HH-mm-ss");
                        string type = Path.GetExtension(file);
                        string fileName = "[" + gameName + "] " + creationTime + "." + type;
                        
                        if (!(File.Exists(screenDir + fileName)))
                        {
                            File.Move(file, screenDir + fileName);
                            logger.Info("Moved and renamed " + file + " to " + screenDir);
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
            // Add code to be executed when Playnite is initialized.
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            // Add code to be executed when library is updated.
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