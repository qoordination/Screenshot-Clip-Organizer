using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace SortMyClips
{
    public class SortMyClipsSettings : ObservableObject
    {
        private string[] _unsortedPaths = Array.Empty<string>();

        public string[] UnsortedPath
        {
            get => _unsortedPaths;
            set => SetValue(ref _unsortedPaths, value);
        }

        private string _unsortedPathString = string.Empty;

        public string UnsortedPathString
        {
            get => _unsortedPathString;
            set => SetValue(ref _unsortedPathString, value);
        }

        [JsonIgnore] private string _unsortedPathInput = string.Empty;

        [JsonIgnore]
        public string UnsortedPathInput
        {
            get => _unsortedPathInput;
            set => SetValue(ref _unsortedPathInput, value);
        }

        private string _sortedPath = string.Empty;

        public string SortedPath
        {
            get => _sortedPath;
            set => SetValue(ref _sortedPath, value);
        }

        private bool _fileModeCopy;

        public bool FileModeCopy
        {
            get => _fileModeCopy;
            set => SetValue(ref _fileModeCopy, value);
        }

        private string _steamPath =
            (Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string);

        public string SteamPath
        {
            get => _steamPath;
            set => SetValue(ref _steamPath, value);
        }

        private string[] _mediaExtensions =
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm" };

        public string[] MediaExtensions
        {
            get => _mediaExtensions;
            set => SetValue(ref _mediaExtensions, value);
        }

        [JsonIgnore] private string _mediaExtensionsInput = string.Empty;

        [JsonIgnore]
        public string MediaExtensionsInput
        {
            get => _mediaExtensionsInput;
            set => SetValue(ref _mediaExtensionsInput, value);
        }

        private string _mediaExtensionsString =
            ".jpg, .jpeg, .png, .bmp, .gif, .mp4, .avi, .mov, .wmv, .flv, .mkv, .webm";

        public string MediaExtensionsString
        {
            get => _mediaExtensionsString;
            set => SetValue(ref _mediaExtensionsString, value);
        }

        private bool _screenshotsMovedCount = true;

        public bool ScreenshotsMovedCount
        {
            get => _screenshotsMovedCount;
            set => SetValue(ref _screenshotsMovedCount, value);
        }

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
    }

    public class SortMyClipsSettingsViewModel : ObservableObject, ISettings
    {
        private readonly ILogger logger = LogManager.GetLogger();
        private readonly SortMyClips plugin;
        private SortMyClipsSettings editingClone { get; set; }

        private SortMyClipsSettings settings;

        public SortMyClipsSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public SortMyClipsSettingsViewModel(SortMyClips plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<SortMyClipsSettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new SortMyClipsSettings();
            }
        }

        public RelayCommand<object> BrowseUnsortedFolder
        {
            get => new RelayCommand<object>((a) =>
            {
                var chosenDir = plugin.PlayniteApi.Dialogs.SelectFolder();
                if (!string.IsNullOrWhiteSpace(chosenDir))
                {
                    Settings.UnsortedPathInput = chosenDir + "\\";
                }

                if (Settings.SortedPath == string.Empty)
                {
                    Settings.SortedPath = Settings.UnsortedPathInput;
                }
            });
        }

        public RelayCommand<object> BrowseSortedFolder
        {
            get => new RelayCommand<object>((a) =>
            {
                var chosenDir = plugin.PlayniteApi.Dialogs.SelectFolder();
                if (!string.IsNullOrWhiteSpace(chosenDir))
                {
                    Settings.SortedPath = chosenDir + "\\";
                }
            });
        }

        public RelayCommand<object> AddUnsortedFolder
        {
            get => new RelayCommand<object>((a) =>
            {
                if (!string.IsNullOrWhiteSpace(Settings.UnsortedPathInput) &&
                    Directory.Exists(Settings.UnsortedPathInput))
                {
                    if (!Settings.UnsortedPath.Contains(Settings.UnsortedPathInput))
                    {
                        var paths = Settings.UnsortedPath.ToList();
                        paths.Add(Settings.UnsortedPathInput);
                        Settings.UnsortedPath = paths.ToArray();
                        logger.Info("Added unsorted path: " + Settings.UnsortedPathInput);
                        Settings.UnsortedPathString = string.Join(Environment.NewLine, Settings.UnsortedPath);
                        Settings.UnsortedPathInput = string.Empty;
                    }
                    else
                    {
                        plugin.PlayniteApi.Dialogs.ShowMessage("This path is already added.");
                    }
                }
                else
                {
                    plugin.PlayniteApi.Dialogs.ShowMessage("Please enter a valid path before adding.");
                }
            });
        }

        public RelayCommand<object> ClearPaths
        {
            get => new RelayCommand<object>((a) =>
            {
                Settings.UnsortedPath = Array.Empty<string>();
                Settings.UnsortedPathString = string.Empty;
                logger.Info("Cleared unsorted paths.");
            });
        }

        public RelayCommand<object> AddSteamPath
        {
            get => new RelayCommand<object>((a) =>
            {
                string steamUserDataPath = Path.Combine(Settings.SteamPath.Replace("/", "\\"), "userdata");
                if (Directory.Exists(steamUserDataPath))
                {
                    string[] userFolders = Directory.GetDirectories(steamUserDataPath);
                    if (userFolders.Length > 0)
                    {
                        foreach (string userFolder in userFolders)
                        {
                            if (Directory.Exists(Path.Combine(userFolder, "760", "remote") + "\\") &&
                                !(Settings.UnsortedPath.Contains(Path.Combine(userFolder, "760", "remote") + "\\")))
                            {
                                var paths = Settings.UnsortedPath.ToList();
                                paths.Add(Path.Combine(userFolder, "760", "remote") + "\\");
                                Settings.UnsortedPath = paths.ToArray();
                                logger.Info("Added steam path: " + Path.Combine(userFolder, "760", "remote") + "\\");
                            }
                            else
                            {
                                logger.Info(
                                    "Already added or no valid steam screenshots folder found in: " + userFolder);
                                plugin.PlayniteApi.Dialogs.ShowMessage(
                                    "Steam path is already added or does not contain Screenshots:\n" +
                                    Path.Combine(userFolder, "760", "remote") +
                                    "\\");
                            }
                        }

                        var newPaths = string.Empty;
                        foreach (string path in Settings.UnsortedPath)
                        {
                            newPaths = Environment.NewLine + path + Environment.NewLine;
                        }

                        Settings.UnsortedPathString += newPaths;
                    }
                    else
                    {
                        plugin.PlayniteApi.Dialogs.ShowMessage("Could not find any valid steam folders.");
                    }
                }
            });
        }

        public RelayCommand<object> RemoveUnsortedFolder
        {
            get => new RelayCommand<object>((a) =>
            {
                if (Directory.Exists(Settings.UnsortedPathInput) &&
                    Settings.UnsortedPath.Contains(Settings.UnsortedPathInput))
                {
                    var paths = Settings.UnsortedPath.ToList();
                    paths.Remove(Settings.UnsortedPathInput);
                    Settings.UnsortedPath = paths.ToArray();
                    logger.Info("Removed unsorted path: " + Settings.UnsortedPathInput);
                    Settings.UnsortedPathString = string.Join(Environment.NewLine, Settings.UnsortedPath);
                    Settings.UnsortedPathInput = string.Empty;
                }
                else if (Settings.UnsortedPathInput == string.Empty)
                {
                    plugin.PlayniteApi.Dialogs.ShowMessage("Please enter a path to remove first.");
                }
                else if (!Directory.Exists(Settings.UnsortedPathInput))
                {
                    plugin.PlayniteApi.Dialogs.ShowMessage("Please enter a valid path to remove.");
                }
                else
                {
                    plugin.PlayniteApi.Dialogs.ShowMessage("This path is not in the list.");
                }
            });
        }

        public RelayCommand<object> AddMediaExtension
        {
            get => new RelayCommand<object>((a) =>
            {
                if (!string.IsNullOrWhiteSpace(Settings.MediaExtensionsInput))
                {
                    string extension = Settings.MediaExtensionsInput.Trim().ToLower();
                    if (!extension.StartsWith("."))
                    {
                        extension = "." + extension;
                    }

                    if (!Settings.MediaExtensions.Contains(extension))
                    {
                        var extensions = Settings.MediaExtensions.ToList();
                        extensions.Add(extension);
                        Settings.MediaExtensions = extensions.ToArray();
                        Settings.MediaExtensionsString = string.Join(", ", Settings.MediaExtensions);
                        logger.Info("Added media extension: " + extension);
                        Settings.MediaExtensionsInput = string.Empty;
                    }
                    else
                    {
                        plugin.PlayniteApi.Dialogs.ShowMessage("This media extension is already added.");
                    }
                }
                else
                {
                    plugin.PlayniteApi.Dialogs.ShowMessage("Please enter a valid media extension before adding.");
                }
            });
        }

        public RelayCommand<object> RemoveMediaExtension
        {
            get => new RelayCommand<object>((a) =>
            {
                if (!string.IsNullOrWhiteSpace(Settings.MediaExtensionsInput))
                {
                    string extension = Settings.MediaExtensionsInput.Trim().ToLower();
                    if (!extension.StartsWith("."))
                    {
                        extension = "." + extension;
                    }

                    if (Settings.MediaExtensions.Contains(extension))
                    {
                        var extensions = Settings.MediaExtensions.ToList();
                        extensions.Remove(extension);
                        Settings.MediaExtensions = extensions.ToArray();
                        Settings.MediaExtensionsString = string.Join(",", Settings.MediaExtensions);
                    }
                    else
                    {
                        plugin.PlayniteApi.Dialogs.ShowMessage("This media extension is not in the list.");
                    }
                }
                else
                {
                    plugin.PlayniteApi.Dialogs.ShowMessage("Please enter a valid media extension to remove.");
                }
            });
        }

        public RelayCommand<object> RestoreDefaultMediaExtension
        {
            get => new RelayCommand<object>((a) =>
            {
                Settings.MediaExtensions = new[]
                {
                    ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm"
                };
                Settings.MediaExtensionsString = string.Join(", ", Settings.MediaExtensions);
                logger.Info("Restored default media extensions.");
            });
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}