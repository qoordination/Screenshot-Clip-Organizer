using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace SortMyClips
{
    public class SortMyClipsSettings : ObservableObject
    {
        private string _unsortedPath = string.Empty;
        public string UnsortedPath
        {
            get => _unsortedPath;
            set => SetValue(ref _unsortedPath, value);
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

        public RelayCommand<object> BrowseFolder
        {
            get => new RelayCommand<object>((a) =>
            {
                var chosenDir = plugin.PlayniteApi.Dialogs.SelectFolder();
                if (!string.IsNullOrWhiteSpace(chosenDir))
                {
                    Settings.UnsortedPath = chosenDir;
                }
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