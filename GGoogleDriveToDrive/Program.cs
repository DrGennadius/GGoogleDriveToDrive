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
        /// <summary>
        /// Google Workspace and Drive MIME Types.<br/>
        /// https://developers.google.com/drive/api/v3/mime-types?hl=en
        /// </summary>
        static readonly string[] GoogleMimeTypes = new string[]
        {
            "application/vnd.google-apps.audio",
            "application/vnd.google-apps.document",
            "application/vnd.google-apps.drive-sdk",
            "application/vnd.google-apps.drawing",
            "application/vnd.google-apps.file",
            "application/vnd.google-apps.folder",
            "application/vnd.google-apps.form",
            "application/vnd.google-apps.fusiontable",
            "application/vnd.google-apps.jam",
            "application/vnd.google-apps.map",
            "application/vnd.google-apps.photo",
            "application/vnd.google-apps.presentation",
            "application/vnd.google-apps.script",
            "application/vnd.google-apps.shortcut",
            "application/vnd.google-apps.site",
            "application/vnd.google-apps.spreadsheet",
            "application/vnd.google-apps.unknown",
            "application/vnd.google-apps.video"
        };

        private const string ClientId = "463415722618-97eb83nbndd7lpdmr5jo7nesd0qnb6na.apps.googleusercontent.com";
        private const string ClientSecret = "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";

        const string ApplicationName = "GGoogleDriveToDrive";
        const string DownloadsFolder = "Downloads";
        const string MimeTypesConvertMapConfigFileName = "MimeTypesConvertMap.json";
        const string LoggingFileName = "logging.txt";
        const string CredentialPath = "Auth.Store";
        
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
            GoogleDriveApiInit();
            Console.WriteLine();
            Processing();
            Console.Read();
        }

        static void GoogleDriveApiInit()
        {
            bool isEmptyAuth = System.IO.File.Exists(Path.Combine(CredentialPath, "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user"));
            Init(isEmptyAuth);
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

        static void Init(bool isRemoveDirectories = false)
        {
            Logging("Start command.");

            using (StreamReader file = System.IO.File.OpenText(MimeTypesConvertMapConfigFileName))
            {
                JsonSerializer serializer = new JsonSerializer();
                MimeTypesConvertMap = (Dictionary<string, ExportTypeConfig>)serializer.Deserialize(file, typeof(Dictionary<string, ExportTypeConfig>));
            }
            if (isRemoveDirectories && Directory.Exists(DownloadsFolder))
            {
                Directory.Delete(DownloadsFolder, true);
            }
            if (!Directory.Exists(DownloadsFolder))
            {
                Directory.CreateDirectory(DownloadsFolder);
            }
#if NET45
            if (isRemoveDirectories && Directory.Exists(DownloadsFolderForLongName))
            {
                Directory.Delete(DownloadsFolderForLongName, true);
            }
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
            IList<Google.Apis.Drive.v3.Data.File> gFiles = fileList.Files;
            Console.WriteLine("Processing...");
            Logging("Processing...");
            int startPartLineCursor = Console.CursorTop;
            while (gFiles != null && gFiles.Count > 0)
            {
                foreach (var gFile in gFiles)
                {
                    ClearLines(startPartLineCursor);
                    Console.SetCursorPosition(0, startPartLineCursor);
                    string itemLine = $"[{++itemsCount}] \"{gFile.Name}\" (Id: \"{gFile.Id}\" {gFile.Size} bytes {gFile.MimeType})";
                    Console.WriteLine(itemLine);
                    Logging(itemLine);
                    PullContentToDrive(gFile);
                }

                listRequest.PageToken = fileList.NextPageToken;
                if (listRequest.PageToken == null)
                {
                    break;
                }
                fileList = listRequest.Execute();
                gFiles = fileList.Files;
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

            bool isGoogleType = GoogleMimeTypes.Contains(gFile.MimeType);
            string fileName = isGoogleType && MimeTypesConvertMap.ContainsKey(gFile.MimeType)
                ? gFile.Name + '.' + MimeTypesConvertMap[gFile.MimeType].FileExtension
                : gFile.Name;
            fileName = MakeValidFileName(fileName);

            string parentId = gFile.Parents?.FirstOrDefault();
            var parent = !string.IsNullOrEmpty(parentId) ? GetParent(parentId) : null;

            string filePath;
            if (parent != null)
            {
                string folderPath = PrepareFolder(parent);
                filePath = Path.Combine(Environment.CurrentDirectory, DownloadsFolder, folderPath, fileName);
            }
            else
            {
                filePath = Path.Combine(Environment.CurrentDirectory, DownloadsFolder, fileName);
            }
#if NET45
            // TODO: Solve this for long name limited.
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

            DownloadingFile = gFile;

            if (isGoogleType)
            {
                // Only if we know what type we are converting to
                if (MimeTypesConvertMap.ContainsKey(gFile.MimeType))
                {
                    using (FileStream file = new FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                    {
                        ExecuteExport(file, gFile, MimeTypesConvertMap[gFile.MimeType].MimeType);
                    }
                }
            }
            else
            {
                // Check Md5Checksum first
                if (!string.IsNullOrWhiteSpace(gFile.Md5Checksum)
                    && System.IO.File.Exists(filePath)
                    && gFile.Md5Checksum.Equals(GetMD5Checksum(filePath)))
                {
                    return;
                }
                using (FileStream file = new FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
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

            Stack<Google.Apis.Drive.v3.Data.File> gFileStack = new Stack<Google.Apis.Drive.v3.Data.File>();
            gFileStack.Push(gFile);
            FillStackByParents(gFile, gFileStack);

            string resultFullPath = "";

#if NET45
            // TODO: Solve this for long name limited.
            // 1. Create full path for limit check.
            foreach (var item in gFileStack)
            {
                resultFullPath = Path.Combine(resultFullPath, MakeValidFileName(item.Name));
            }
            resultFullPath = Path.Combine(Environment.CurrentDirectory, DownloadsFolder, resultFullPath);
            // 2. Limit check.
            bool isTruncated = false;
            if (resultFullPath.Length >= MAX_DIRECTORY_PATH)
            {
                resultFullPath = Path.Combine(Environment.CurrentDirectory, DownloadsFolderForLongName, MakeValidFileName(gFile.Name));
                isTruncated = true;
            }
            if (resultFullPath.Length >= MAX_DIRECTORY_PATH)
            {
                string folderName = MakeValidFileName(gFile.Name);
                int prefixPathLength = resultFullPath.Length - folderName.Length;
                folderName = folderName.Substring(0, MAX_DIRECTORY_PATH - prefixPathLength);
                resultFullPath = Path.Combine(Environment.CurrentDirectory, DownloadsFolderForLongName, folderName);
            }
            if (isTruncated)
            {
                CreateDirectory(gFile, resultFullPath);
                return resultFullPath;
            }
#endif

            CreateDirectories(gFile, gFileStack);
            return resultFullPath;
        }

        static void CreateDirectories(Google.Apis.Drive.v3.Data.File gFile, Stack<Google.Apis.Drive.v3.Data.File> gFileStack)
        {
            string subPath = Path.Combine(Environment.CurrentDirectory, DownloadsFolder);
            foreach (var item in gFileStack)
            {
                subPath = Path.Combine(subPath, MakeValidFileName(item.Name));
                CreateDirectory(gFile, subPath);
            }
        }

        static void CreateDirectory(Google.Apis.Drive.v3.Data.File gFile, string resultFullPath)
        {
            if (!Directory.Exists(resultFullPath))
            {
                var directoryInfo = Directory.CreateDirectory(resultFullPath);
                if (gFile.CreatedTime.HasValue)
                {
                    directoryInfo.CreationTime = gFile.CreatedTime.Value;
                }
            }
        }

        static void FillStackByParents(Google.Apis.Drive.v3.Data.File gFile, Stack<Google.Apis.Drive.v3.Data.File> gFileStack)
        {
            if (gFile.Parents != null && gFile.Parents.Count >= 0)
            {
                var parent = gFile;
                while (true)
                {
                    parent = GetParent(parent.Parents[0]);

                    // Stop when we find the root dir
                    if (parent.Parents == null || parent.Parents.Count == 0)
                    {
                        break;
                    }

                    gFileStack.Push(parent);
                }
            }
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
            FilesCache.Add(id, parent);

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
