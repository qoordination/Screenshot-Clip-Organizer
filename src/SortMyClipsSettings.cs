using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace SortMyClips
{
    public class SortMyClipsSettings : ObservableObject
    {
        private ObservableCollection<string> _unsortedPaths = new ObservableCollection<string>();

        public ObservableCollection<string> UnsortedPaths
        {
            get => _unsortedPaths;
            set => SetValue(ref _unsortedPaths, value);
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

        private ObservableCollection<string> _mediaExtensions =
            new ObservableCollection<string>
            {
                ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4", ".avi", ".mov", ".wmv",
                ".flv", ".mkv", ".webm"
            };

        public ObservableCollection<string> MediaExtensions
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

        private bool _displayAmountMoved = true;

        public bool DisplayAmountMoved
        {
            get => _displayAmountMoved;
            set => SetValue(ref _displayAmountMoved, value);
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

        public RelayCommand<object> BrowseUnsortedFolder { get; }
        public RelayCommand<object> AddUnsortedFolder { get; }
        public RelayCommand<object> RemoveUnsortedFolder { get; }
        public RelayCommand<object> BrowseSortedFolder { get; }
        public RelayCommand<object> AddSteamPaths { get; }
        public RelayCommand<object> ClearUnsortedPaths { get; }
        public RelayCommand<object> AddMediaExtension { get; }
        public RelayCommand<object> RemoveMediaExtension { get; }
        public RelayCommand<object> ResetMediaExtensions { get; }

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

            BrowseUnsortedFolder = new RelayCommand<object>(_ => BrowseUnsortedFolderImpl());
            AddUnsortedFolder = new RelayCommand<object>(_ => AddUnsortedFolderImpl());
            RemoveUnsortedFolder = new RelayCommand<object>(path => RemoveUnsortedFolderImpl(path));
            BrowseSortedFolder = new RelayCommand<object>(_ => BrowseSortedFolderImpl());
            AddSteamPaths = new RelayCommand<object>(_ => AddSteamPathsImpl());
            ClearUnsortedPaths = new RelayCommand<object>(_ => ClearUnsortedPathsImpl());
            AddMediaExtension = new RelayCommand<object>(_ => AddMediaExtensionImpl());
            RemoveMediaExtension = new RelayCommand<object>(_ => RemoveMediaExtensionImpl());
            ResetMediaExtensions = new RelayCommand<object>(_ => ResetMediaExtensionsImpl());
        }

        private void BrowseUnsortedFolderImpl()
        {
            var chosenDir = plugin.PlayniteApi.Dialogs.SelectFolder();
            if (string.IsNullOrWhiteSpace(chosenDir))
            {
                return;
            }

            Settings.UnsortedPathInput = chosenDir;

            if (string.IsNullOrEmpty(Settings.SortedPath) || string.IsNullOrWhiteSpace(Settings.SortedPath))
            {
                Settings.SortedPath = Settings.UnsortedPathInput;
            }
        }

        private void AddUnsortedFolderImpl()
        {
            var path = Settings.UnsortedPathInput;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("Please enter a valid path before adding.");
                return;
            }

            if (Settings.UnsortedPaths.Contains(path))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("This path is already added.");
                return;
            }

            Settings.UnsortedPaths.Add(path);
            logger.Info("Added unsorted path: " + path);
            Settings.UnsortedPathInput = string.Empty;
        }

        private void RemoveUnsortedFolderImpl(object parameter)
        {
            if (!(parameter is string path))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("Invalid path provided.");
                return;
            }
            
            if (Directory.Exists(path) &&
                Settings.UnsortedPaths.Contains(path))
            {
                Settings.UnsortedPaths.Remove(path);
                logger.Info("Removed unsorted path: " + path);
            }
            else if (string.IsNullOrEmpty(path))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("Please enter a path to remove first.");
            }
            else if (!Directory.Exists(path))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("Please enter a valid path to remove.");
            }
            else
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("This path is not in the list.");
            }
        }

        private void BrowseSortedFolderImpl()
        {
            var chosenDir = plugin.PlayniteApi.Dialogs.SelectFolder();
            if (!string.IsNullOrWhiteSpace(chosenDir))
            {
                Settings.SortedPath = chosenDir;
            }
        }

        private void AddSteamPathsImpl()
        {
            var steamUserDataPath = (Settings.SteamPath != null)
                ? Path.Combine(Settings.SteamPath.Replace("/", "\\"), "userdata")
                : string.Empty;
            if (!Directory.Exists(steamUserDataPath))
            {
                logger.Info("Could not find steam userdata folder at: " + steamUserDataPath);
                return;
            }

            string[] userFolders = Directory.GetDirectories(steamUserDataPath);
            if (userFolders.Length > 0)
            {
                foreach (var userFolder in userFolders)
                {
                    if (Directory.Exists(Path.Combine(userFolder, "760", "remote")) &&
                        !(Settings.UnsortedPaths.Contains(Path.Combine(userFolder, "760", "remote"))))
                    {
                        Settings.UnsortedPaths.Add(Path.Combine(userFolder, "760", "remote"));
                        logger.Info("Added steam path: " + Path.Combine(userFolder, "760", "remote"));
                    }
                    else
                    {
                        logger.Info(
                            "Already added or no valid steam screenshots folder found in: " + userFolder);
                        plugin.PlayniteApi.Dialogs.ShowMessage(
                            "Steam path is already added or does not contain Screenshots:\n" +
                            Path.Combine(userFolder, "760", "remote"));
                    }
                }
            }
            else
            {
                plugin.PlayniteApi.Dialogs.ShowMessage("Could not find any valid steam folders.");
            }
        }

        private void ClearUnsortedPathsImpl()
        {
            Settings.UnsortedPaths.Clear();
            logger.Info("Cleared unsorted paths.");
        }

        private void AddMediaExtensionImpl()
        {
            if (!string.IsNullOrWhiteSpace(Settings.MediaExtensionsInput))
            {
                string extension = Settings.MediaExtensionsInput.Trim().ToLowerInvariant();
                if (!extension.StartsWith("."))
                {
                    extension = "." + extension;
                }

                if (!Settings.MediaExtensions.Contains(extension))
                {
                    Settings.MediaExtensions.Add(extension);
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
        }

        private void RemoveMediaExtensionImpl()
        {
            if (!string.IsNullOrWhiteSpace(Settings.MediaExtensionsInput))
            {
                string extension = Settings.MediaExtensionsInput.Trim().ToLowerInvariant();
                if (!extension.StartsWith("."))
                {
                    extension = "." + extension;
                }

                if (Settings.MediaExtensions.Contains(extension))
                {
                    Settings.MediaExtensions.Remove(extension);
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
        }

        private void ResetMediaExtensionsImpl()
        {
            Settings.MediaExtensions = new ObservableCollection<string>
            {
                ".jpg", ".jpeg", ".png", ".bmp", ".gif",
                ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm"
            };
            logger.Info("Restored default media extensions.");
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