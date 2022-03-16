using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace GGoogleDriveToDrive.AppConfiguration
{
    public class AppConfig
    {
        [JsonIgnore]
        public string AppConfigurationFileName { get; private set; }

        public Dictionary<string, ExportTypeConfig> MimeTypesConvertMap { get; set; }

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

        internal static AppConfig CreateByMimeTypesConvertMap(Dictionary<string, ExportTypeConfig> mimeTypesConvertMap)
        {
            return new AppConfig()
            {
                MimeTypesConvertMap = mimeTypesConvertMap
            };
        }

        internal static void Save(AppConfig appConfiguration, string path)
        {
            if (string.IsNullOrEmpty(appConfiguration.AppConfigurationFileName))
            {
                appConfiguration.AppConfigurationFileName = path;
            }
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
}
