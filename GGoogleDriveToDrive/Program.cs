﻿using Google.Apis.Auth.OAuth2;
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
        const string LoggingFileName = "logging.txt";
        const string CredentialPath = "Auth.Store";
        private const string ClientId = "463415722618-97eb83nbndd7lpdmr5jo7nesd0qnb6na.apps.googleusercontent.com";
        private const string ClientSecret = "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";
        static readonly string[] Scopes = { DriveService.Scope.DriveReadonly };
        static DriveService Service;
        static FilesResource FilesProvider;
        static Dictionary<string, Google.Apis.Drive.v3.Data.File> FilesCache = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();
        static Google.Apis.Drive.v3.Data.File DownloadingFile;

        static Dictionary<string, ExportTypeConfig> MimeTypesConvertMap;

#if NET45
        const int MAX_PATH = 260;
        const int MAX_DIRECTORY_PATH = 248;
        const string DownloadsFolderForLongName = "WithLongNames";
#endif

        static void Main(string[] args)
        {
            Init();
            GoogleDriveApiInit();
            Console.WriteLine();
            Processing();
            Console.Read();
        }

        static void GoogleDriveApiInit()
        {
            UserCredential credential;
            ClientSecrets clientSecrets = System.IO.File.Exists("client_secrets.json")
                ? GoogleClientSecrets.FromFile("client_secrets.json").Secrets
                : new ClientSecrets
                {
                    ClientId = ClientId,
                    ClientSecret = ClientSecret
                };
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(clientSecrets,
                                                                     Scopes,
                                                                     "user",
                                                                     CancellationToken.None,
                                                                     new FileDataStore(CredentialPath, true)).Result;

            Console.WriteLine("Credential file saved to: " + CredentialPath);

            // Create Drive API service.
            Service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            FilesProvider = Service.Files;
        }

        static void Init()
        {
            Logging("Start command.");

            using (StreamReader file = System.IO.File.OpenText(MimeTypesConvertMapConfigFileName))
            {
                JsonSerializer serializer = new JsonSerializer();
                MimeTypesConvertMap = (Dictionary<string, ExportTypeConfig>)serializer.Deserialize(file, typeof(Dictionary<string, ExportTypeConfig>));
            }
            if (!Directory.Exists(DownloadsFolder))
            {
                Directory.CreateDirectory(DownloadsFolder);
            }
#if NET45
            if (!Directory.Exists(DownloadsFolderForLongName))
            {
                Directory.CreateDirectory(DownloadsFolderForLongName);
            }
#endif
        }

        static void Processing()
        {
            // Define parameters of request.
            FilesResource.ListRequest listRequest = FilesProvider.List();
            listRequest.PageSize = 100;
            listRequest.Fields = "nextPageToken, files(id, name, originalFilename, createdTime, modifiedTime, mimeType, size, md5Checksum, parents)";

            int startLineCursor = Console.CursorTop;
            int itemsCount = 0;

            FileList fileList = listRequest.Execute();
            IList<Google.Apis.Drive.v3.Data.File> files = fileList.Files;
            Console.WriteLine("Processing...");
            Logging("Processing...");
            int startPartLineCursor = Console.CursorTop;
            while (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    ClearLines(startPartLineCursor);
                    Console.SetCursorPosition(0, startPartLineCursor);
                    string itemLine = $"[{++itemsCount}] \"{file.Name}\" (Id: \"{file.Id}\" {file.Size} bytes {file.MimeType})";
                    Console.WriteLine(itemLine);
                    Logging(itemLine);
                    PullContentToDrive(file);
                }

                listRequest.PageToken = fileList.NextPageToken;
                if (listRequest.PageToken == null)
                {
                    break;
                }
                fileList = listRequest.Execute();
                files = fileList.Files;
            }

            ClearLines(startLineCursor);
            FinishPrint(itemsCount);
        }

        static void FinishPrint(int itemsCount)
        {
            Console.WriteLine("Done!");
            Logging("Done!");
            Console.WriteLine($"Processed {itemsCount} items.");
            Logging($"Processed {itemsCount} items.");
        }

        static void ClearLines(int startLineCursor)
        {
            int currentLineCursor = Console.CursorTop;
            for (int lineN = startLineCursor; lineN < currentLineCursor; lineN++)
            {
                Console.SetCursorPosition(0, lineN);
                for (int i = 0; i < Console.WindowWidth; i++)
                    Console.Write(" ");
            }
        }

        static void PullContentToDrive(Google.Apis.Drive.v3.Data.File gFile)
        {
            if (gFile.MimeType == "application/vnd.google-apps.folder")
            {
                PrepareFolder(gFile);
                return;
            }

            string fileName = MimeTypesConvertMap.ContainsKey(gFile.MimeType)
                ? gFile.Name + '.' + MimeTypesConvertMap[gFile.MimeType].FileExtension
                : gFile.Name;
            fileName = MakeValidFileName(fileName);

            string parentId = gFile.Parents?.FirstOrDefault();
            var parent = !string.IsNullOrEmpty(parentId) ? GetParent(parentId) : null;

            string filePath;
            if (parent != null)
            {
                string folderPath = PrepareFolder(parent);
                filePath = Path.Combine(folderPath, fileName);
            }
            else
            {
                filePath = Path.Combine(DownloadsFolder, fileName);
            }
#if NET45
            // TODO: Solve this for long name limited.
            filePath = Path.Combine(Environment.CurrentDirectory, filePath);
            if (filePath.Length >= MAX_PATH)
            {
                filePath = Path.Combine(Environment.CurrentDirectory, DownloadsFolderForLongName, fileName);
            }
            if (filePath.Length >= MAX_PATH)
            {
                int fullLength = filePath.Length;
                string fileExtention = Path.GetExtension(filePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                int prefixPathLenth = fullLength - fileNameWithoutExt.Length - fileExtention.Length;
                fileNameWithoutExt = fileNameWithoutExt.Substring(0, MAX_PATH - prefixPathLenth - fileExtention.Length);
                filePath = Path.Combine(Environment.CurrentDirectory, DownloadsFolderForLongName, fileNameWithoutExt + fileExtention);
            }
#endif

            if (!string.IsNullOrWhiteSpace(gFile.Md5Checksum)
                && System.IO.File.Exists(filePath)
                && gFile.Md5Checksum.Equals(GetMD5Checksum(filePath)))
            {
                return;
            }

            DownloadingFile = gFile;

            using (FileStream file = new FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            {
                if (MimeTypesConvertMap.ContainsKey(gFile.MimeType))
                {
                    ExecuteExport(file, gFile, MimeTypesConvertMap[gFile.MimeType].MimeType);
                }
                else
                {
                    ExecuteDownload(file, gFile);
                }
            }
        }

        static void ExecuteExport(FileStream fileStream, Google.Apis.Drive.v3.Data.File gFile, string mimeType)
        {
            var request = FilesProvider.Export(gFile.Id, mimeType);
            request.MediaDownloader.ProgressChanged += Download_ProgressChanged;
            var downloadProgress = request.DownloadWithStatus(fileStream);
            CorrectFileOnFS(fileStream, gFile, downloadProgress.Status);
        }

        static void ExecuteDownload(FileStream fileStream, Google.Apis.Drive.v3.Data.File gFile)
        {
            var request = FilesProvider.Get(gFile.Id);
            request.MediaDownloader.ProgressChanged += Download_ProgressChanged;
            var downloadProgress = request.DownloadWithStatus(fileStream);
            CorrectFileOnFS(fileStream, gFile, downloadProgress.Status);
        }

        static void CorrectFileOnFS(FileStream fileStream, Google.Apis.Drive.v3.Data.File gFile, DownloadStatus downloadStatus)
        {
            fileStream.Close();
            if (downloadStatus == DownloadStatus.Failed)
            {
                System.IO.File.Delete(fileStream.Name);
            }
            else
            {
                FileInfo fileInfo = new FileInfo(fileStream.Name);
                if (gFile.CreatedTime.HasValue)
                {
                    fileInfo.CreationTime = gFile.CreatedTime.Value;
                }
                if (gFile.ModifiedTime.HasValue)
                {
                    fileInfo.LastWriteTime = gFile.ModifiedTime.Value;
                }
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
#if NET45
            // TODO: Solve this for long name limited.
            path = Path.Combine(Environment.CurrentDirectory, path);
            if (path.Length >= MAX_DIRECTORY_PATH)
            {
                path = Path.Combine(Environment.CurrentDirectory, DownloadsFolderForLongName, MakeValidFileName(gFile.Name));
            }
            if (path.Length >= MAX_DIRECTORY_PATH)
            {
                string folderName = MakeValidFileName(gFile.Name);
                int prefixPathLength = path.Length - folderName.Length;
                folderName = folderName.Substring(0, MAX_DIRECTORY_PATH - prefixPathLength);
                path = Path.Combine(Environment.CurrentDirectory, DownloadsFolderForLongName, folderName);
            }
#endif
            if (!Directory.Exists(path))
            {
                var directoryInfo = Directory.CreateDirectory(path);
                if (gFile.CreatedTime.HasValue)
                {
                    directoryInfo.CreationTime = gFile.CreatedTime.Value;
                }
                if (gFile.ModifiedTime.HasValue)
                {
                    directoryInfo.LastWriteTime = gFile.ModifiedTime.Value;
                }
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

            Stack<string> pathQueue = new Stack<string>();
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
            request.Fields = "id, name, createdTime, modifiedTime, parents";
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

        static string GetMD5Checksum(string fileName)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = System.IO.File.OpenRead(fileName))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLower();
                }
            }
        }

        static void Download_ProgressChanged(IDownloadProgress progress)
        {
            switch (progress.Status)
            {
                case DownloadStatus.NotStarted:
                    break;
                case DownloadStatus.Downloading:
                    Logging($"{progress.Status} {progress.BytesDownloaded} bytes.");
                    break;
                case DownloadStatus.Completed:
                    Logging($"{progress.Status} {progress.BytesDownloaded} bytes.");
                    break;
                case DownloadStatus.Failed:
                    Logging($"{progress.Status}{Environment.NewLine}{progress.Exception}");
                    break;
                default:
                    break;
            }
        }

        static void Logging(string message)
        {
            using (StreamWriter writer = new StreamWriter(LoggingFileName, true))
            {
                writer.WriteLine($"{DateTime.Now} {message}");
            }
        }
    }
}
