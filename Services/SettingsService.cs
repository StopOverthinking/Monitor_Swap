using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;
using MonitorSwap.Models;

namespace MonitorSwap.Services
{
    internal sealed class SettingsService
    {
        private const string ApplicationRegistryKeyPath = @"Software\MonitorSwap";
        private const string UiLanguageRegistryValueName = "UiLanguage";
        private readonly string _settingsPath;

        public SettingsService()
        {
            var appDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonitorSwap");
            _settingsPath = Path.Combine(appDataDirectory, "settings.json");
        }

        public AppSettings Load()
        {
            AppSettings settings = null;

            if (File.Exists(_settingsPath))
            {
                try
                {
                    using (var stream = File.OpenRead(_settingsPath))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                        settings = (AppSettings)serializer.ReadObject(stream);
                    }
                }
                catch
                {
                    settings = null;
                }
            }

            if (settings == null)
            {
                settings = new AppSettings();
            }

            settings.EnsureDefaults();
            ApplyDefaultLanguage(settings);
            return settings;
        }

        public void Save(AppSettings settings)
        {
            settings.EnsureDefaults();
            ApplyDefaultLanguage(settings);
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = File.Create(_settingsPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                serializer.WriteObject(stream, settings);
            }

            PersistPreferredLanguage(settings);
        }

        private static void ApplyDefaultLanguage(AppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.UiLanguageCode))
            {
                return;
            }

            settings.SetUiLanguage(ResolveDefaultLanguage());
        }

        private static AppLanguage ResolveDefaultLanguage()
        {
            var installerLanguage = ReadDefaultLanguageFromRegistry();
            if (installerLanguage.HasValue)
            {
                return installerLanguage.Value;
            }

            return AppLanguageExtensions.FromCulture(CultureInfo.CurrentUICulture);
        }

        private static AppLanguage? ReadDefaultLanguageFromRegistry()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(ApplicationRegistryKeyPath))
            {
                var languageCode = key != null ? key.GetValue(UiLanguageRegistryValueName) as string : null;
                if (string.IsNullOrWhiteSpace(languageCode))
                {
                    return null;
                }

                return AppLanguageExtensions.FromCode(languageCode);
            }
        }

        private static void PersistPreferredLanguage(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.UiLanguageCode))
            {
                return;
            }

            using (var key = Registry.CurrentUser.CreateSubKey(ApplicationRegistryKeyPath))
            {
                if (key != null)
                {
                    key.SetValue(UiLanguageRegistryValueName, settings.UiLanguageCode);
                }
            }
        }
    }
}
