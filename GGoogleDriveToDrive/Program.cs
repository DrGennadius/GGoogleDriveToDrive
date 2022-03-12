using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace GGoogleDriveToDrive
{
    class Program
    {
        const string ApplicationName = "GGoogleDriveToDrive";
        const string DownloadsFolder = "Downloads";
        const string MimeTypesConvertMapConfigFileName = "MimeTypesConvertMap.json";
        static readonly string[] Scopes = { DriveService.Scope.DriveReadonly };
        static DriveService Service;
        static FilesResource FilesProvider;
        static Dictionary<string, Google.Apis.Drive.v3.Data.File> FilesCache = new();
        static Google.Apis.Drive.v3.Data.File DownloadingFile;

        static Dictionary<string, ExportTypeConfig> MimeTypesConvertMap;

        static void Main(string[] args)
        {
            using (StreamReader file = System.IO.File.OpenText(MimeTypesConvertMapConfigFileName))
            {
                JsonSerializer serializer = new();
                MimeTypesConvertMap = (Dictionary<string, ExportTypeConfig>)serializer.Deserialize(file, typeof(Dictionary<string, ExportTypeConfig>));
            }

            UserCredential credential;

            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "Auth.Store";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Drive API service.
            Service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            if (Directory.Exists(DownloadsFolder))
            {
                Directory.Delete(DownloadsFolder, true);
            }
            Directory.CreateDirectory(DownloadsFolder);

            FilesProvider = Service.Files;

            // Define parameters of request.
            FilesResource.ListRequest listRequest = FilesProvider.List();
            listRequest.PageSize = 100;
            listRequest.Fields = "nextPageToken, files(id, name, originalFilename, mimeType, size, parents)";

            FileList fileList = listRequest.Execute();
            IList<Google.Apis.Drive.v3.Data.File> files = fileList.Files;
            while (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    Console.WriteLine($"{file.Id} {file.Size} {file.MimeType} \"{file.Name}\"");
                    PullContentToDrive(file);
                }

                listRequest.PageToken = fileList.NextPageToken;
                fileList = listRequest.Execute();
                files = fileList.Files;
            }
            Console.Read();
        }

        static void PullContentToDrive(Google.Apis.Drive.v3.Data.File gFile)
        {
            if (gFile.MimeType == "application/vnd.google-apps.folder")
            {
                PrepareFolder(gFile);
                return;
            }

            string filePath = "";
            string fileName = "";
            if (MimeTypesConvertMap.ContainsKey(gFile.MimeType))
            {
                var typeConfig = MimeTypesConvertMap[gFile.MimeType];
                fileName = gFile.Name + '.' + typeConfig.FileExtension;
            }
            else
            {
                fileName = gFile.Name;
            }
            fileName = MakeValidFileName(fileName);

            string parentId = gFile.Parents?.FirstOrDefault();
            var parent = !string.IsNullOrEmpty(parentId) ? GetParent(parentId) : null;
            if (parent != null)
            {
                string folderPath = PrepareFolder(parent);
                filePath = Path.Combine(folderPath, fileName);
            }
            else
            {
                filePath = Path.Combine(DownloadsFolder, fileName);
            }

            DownloadingFile = gFile;
            using FileStream file = new(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write);

            if (MimeTypesConvertMap.ContainsKey(gFile.MimeType))
            {
                var typeConfig = MimeTypesConvertMap[gFile.MimeType];
                var request = FilesProvider.Export(gFile.Id, typeConfig.MimeType);
                request.MediaDownloader.ProgressChanged += Download_ProgressChanged;
                request.Download(file);
            }
            else
            {
                var request = FilesProvider.Get(gFile.Id);
                request.MediaDownloader.ProgressChanged += Download_ProgressChanged;
                request.Download(file);
            }
        }

        static string PrepareFolder(Google.Apis.Drive.v3.Data.File gFile)
        {
            // Check and add to cache
            if (!FilesCache.ContainsKey(gFile.Id))
            {
                FilesCache.Add(gFile.Id, gFile);
            }
            string path = Path.Combine(DownloadsFolder, GetGoogleAbsPath(gFile));
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        static string GetGoogleAbsPath(Google.Apis.Drive.v3.Data.File file)
        {
            var name = MakeValidFileName(file.Name);

            if (file.Parents == null || file.Parents.Count == 0)
            {
                return name;
            }

            Stack<string> pathQueue = new();
            pathQueue.Push(name);

            while (true)
            {
                var parent = GetParent(file.Parents[0]);

                // Stop when we find the root dir
                if (parent.Parents == null || parent.Parents.Count == 0)
                {
                    break;
                }

                pathQueue.Push(MakeValidFileName(parent.Name));
                file = parent;
            }
            return pathQueue.Aggregate((current, next) => Path.Combine(current, next));
        }

        static Google.Apis.Drive.v3.Data.File GetParent(string id)
        {
            // Check cache
            if (FilesCache.ContainsKey(id))
            {
                return FilesCache[id];
            }

            // Fetch file from drive
            var request = FilesProvider.Get(id);
            request.Fields = "id,name,parents";
            var parent = request.Execute();

            // Save in cache
            FilesCache[id] = parent;

            return parent;
        }

        static string MakeValidFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return Regex.Replace(name, invalidRegStr, "_");
        }

        static void Download_ProgressChanged(IDownloadProgress progress)
        {
            switch (progress.Status)
            {
                case DownloadStatus.NotStarted:
                    break;
                case DownloadStatus.Downloading:
                    Console.WriteLine(progress.Status + " " + progress.BytesDownloaded);
                    break;
                case DownloadStatus.Completed:
                    Console.WriteLine(progress.Status + " " + progress.BytesDownloaded);
                    break;
                case DownloadStatus.Failed:
                    Console.WriteLine(progress.Status + "\n" + progress.Exception.ToString());
                    break;
                default:
                    break;
            }
        }
    }
}
