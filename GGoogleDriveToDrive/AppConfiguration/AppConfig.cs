using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;

namespace GGoogleDriveToDrive.AppConfiguration
{
    /// <summary>
    /// Application configuration.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Application configuration file name.
        /// </summary>
        [JsonIgnore]
        public string AppConfigurationFileName { get; private set; }

        /// <summary>
        /// Local directory for downloading and exporting files from Google Drive.
        /// </summary>
        public string DownloadsFolder { get; set; }

        /// <summary>
        /// Defines the scope of the loaded content. Possible options: All, IAmOwnerOnly, MyDriveOnly.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ContentPullMode ContentPullMode { get; set; } = ContentPullMode.All;

        /// <summary>
        /// Used to convert types from Google to another when exporting.
        /// </summary>
        public Dictionary<string, ExportTypeConfig> MimeTypesConvertMap { get; set; }

        /// <summary>
        /// Load the application configuration from the file path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static AppConfig Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
            }
            if (!File.Exists(path))
            {
                throw new ArgumentException($"The file at the specified path '{path}' does not exist.");
            }

            using (var file = File.OpenText(path))
            {
                JsonSerializer serializer = new JsonSerializer();
                var appConfiguration = (AppConfig)serializer.Deserialize(file, typeof(AppConfig));
                return appConfiguration;
            }
        }

        /// <summary>
        /// Create application configuration from the MimeTypesConvertMap instance. For support old config.
        /// </summary>
        /// <param name="mimeTypesConvertMap"></param>
        /// <returns></returns>
        [Obsolete("For old config.")]
        internal static AppConfig CreateByMimeTypesConvertMap(Dictionary<string, ExportTypeConfig> mimeTypesConvertMap)
        {
            return new AppConfig()
            {
                MimeTypesConvertMap = mimeTypesConvertMap
            };
        }

        /// <summary>
        /// Save the application configuration to the file by the path.
        /// </summary>
        /// <param name="appConfiguration"></param>
        /// <param name="path"></param>
        internal static void Save(AppConfig appConfiguration, string path)
        {
            appConfiguration.AppConfigurationFileName = path;
            using (StreamWriter file = File.CreateText(path))
            {
                JsonSerializer serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(file, appConfiguration);
            }
        }
    }

    /// <summary>
    /// Content pull mode.
    /// </summary>
    public enum ContentPullMode
    {
        /// <summary>
        /// All available content.
        /// </summary>
        All,

        /// <summary>
        /// Files that have me as the owner.
        /// </summary>
        IAmOwnerOnly,

        /// <summary>
        /// Files that are only on my drive.
        /// </summary>
        MyDriveOnly
    }
}
