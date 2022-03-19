using GGoogleDriveToDrive.DataBase;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using GGoogleDriveToDrive.Models;
using System.Text.RegularExpressions;
using System.Linq;
using Google.Apis.Download;
using GGoogleDriveToDrive.AppConfiguration;

#if NET45
using Alphaleonis.Win32.Filesystem;
#else
using System.IO;
#endif

namespace GGoogleDriveToDrive.Services
{
    public class GoogleDriveManager
    {
        private const string ClientId = "463415722618-97eb83nbndd7lpdmr5jo7nesd0qnb6na.apps.googleusercontent.com";
        private const string ClientSecret = "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";

        private DriveService Service;
        private FilesResource FilesProvider;
        private Dictionary<string, Google.Apis.Drive.v3.Data.File> FilesCache = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();
        private DbContext DbContext = new DbContext();
        private PullContentProgress PullContentProgress = new PullContentProgress();

        /// <summary>
        /// Google Workspace and Drive MIME Types.<br/>
        /// https://developers.google.com/drive/api/v3/mime-types?hl=en
        /// </summary>
        public readonly string[] GoogleMimeTypes = new string[]
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

        public const string ApplicationName = "GGoogleDriveToDrive";

        public string DownloadsFolder { get; set; } = "Downloads";

        [Obsolete("Use new property 'AppConfigurationFileName' for new configuration.")]
        public const string MimeTypesConvertMapConfigFileName = "MimeTypesConvertMap.json";

        public const string AppConfigurationFileName = "app_config.json";

        public const string CredentialPath = "Auth.Store";

        public readonly string[] Scopes = { DriveService.Scope.DriveReadonly };

        public Google.Apis.Drive.v3.Data.File DownloadingFile { get; private set; }

        public AppConfig AppConfiguration { get; private set; }

        public Dictionary<string, ExportTypeConfig> MimeTypesConvertMap { get; private set; }

        public event Action<PullContentProgress> ProgressChanged;

        public void Initialize()
        {
            try
            {
                if (System.IO.File.Exists(MimeTypesConvertMapConfigFileName) && !System.IO.File.Exists(AppConfigurationFileName))
                {
                    // TODO: Get rid of this branch.
                    using (var file = System.IO.File.OpenText(MimeTypesConvertMapConfigFileName))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        MimeTypesConvertMap = (Dictionary<string, ExportTypeConfig>)serializer.Deserialize(file, typeof(Dictionary<string, ExportTypeConfig>));
                        AppConfiguration = AppConfig.CreateByMimeTypesConvertMap(MimeTypesConvertMap);
                        AppConfiguration.DownloadsFolder = DownloadsFolder;
                        AppConfig.Save(AppConfiguration, AppConfigurationFileName);
                    }
                }
                else
                {
                    AppConfiguration = AppConfig.Load(AppConfigurationFileName);
                    MimeTypesConvertMap = AppConfiguration.MimeTypesConvertMap;
                    if (string.IsNullOrWhiteSpace(AppConfiguration.DownloadsFolder))
                    {
                        AppConfiguration.DownloadsFolder = DownloadsFolder;
                    }
                    else
                    {
                        DownloadsFolder = AppConfiguration.DownloadsFolder;
                    }
                    AppConfig.Save(AppConfiguration, AppConfigurationFileName);
                }
                bool isEmptyAuth = !System.IO.File.Exists(Path.Combine(CredentialPath, "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user"));
                PreparyDirectories(isEmptyAuth);
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

                // Create Drive API service.
                Service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });

                FilesProvider = Service.Files;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Initialization failed:");
                Console.WriteLine(ex);
            }
            
        }

        public PullContentProgress PullContent()
        {
            PullContentProgress = new PullContentProgress
            {
                Status = PullContentProgressStatus.Processing
            };
            try
            {
                // Define parameters of request.
                FilesResource.ListRequest listRequest = FilesProvider.List();
                listRequest.PageSize = 100;
                if (AppConfiguration.ContentPullMode == ContentPullMode.IAmOwnerOnly)
                {
                    listRequest.Q = "'me' in owners";
                }
                else if (AppConfiguration.ContentPullMode == ContentPullMode.MyDriveOnly)
                {
                    listRequest.Q = "'root' in parents";
                }
                listRequest.Fields = "nextPageToken, files(id, name, originalFilename, createdTime, modifiedTime, mimeType, size, md5Checksum, capabilities, parents)";

                Google.Apis.Drive.v3.Data.FileList fileList = listRequest.Execute();
                IList<Google.Apis.Drive.v3.Data.File> gFiles = fileList.Files;
                while (gFiles != null && gFiles.Count > 0)
                {
                    foreach (var gFile in gFiles)
                    {
                        PullContentProgress.ItemsCount++;
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
                PullContentProgress.Status = PullContentProgressStatus.Completed;
            }
            catch (Exception ex)
            {
                PullContentProgress.Status = PullContentProgressStatus.Failed;
                PullContentProgress.Exception = ex;
            }

            return PullContentProgress;
        }

        private void PullContentToDrive(Google.Apis.Drive.v3.Data.File gFile)
        {
            PullContentProgress.InitNewPulling(gFile);
            if (gFile.MimeType == "application/vnd.google-apps.folder")
            {
                GoogleFileInfo googleFileInfo = DbContext.GoogleFiles.Query().SingleOrDefault(x => x.GoogleId == gFile.Id);
                if (googleFileInfo != null
                    && Directory.Exists(Path.Combine(DownloadsFolder, googleFileInfo.LocalPath)))
                {
                    PullContentProgress.CurrentPullingStatus = CurrentPullingStatus.AlreadyPreparedFolder;
                    ProgressChanged?.Invoke(PullContentProgress);
                    return;
                }
                PrepareFolder(gFile);
                PullContentProgress.CurrentPullingStatus = CurrentPullingStatus.PreparedFolder;
                ProgressChanged?.Invoke(PullContentProgress);
                return;
            }

            bool isGoogleType = GoogleMimeTypes.Contains(gFile.MimeType);
            if (isGoogleType && gFile.ModifiedTime.HasValue)
            {
                GoogleFileInfo googleFileInfo = DbContext.GoogleFiles.Query().SingleOrDefault(x => x.GoogleId == gFile.Id);
                if (googleFileInfo != null
                    && googleFileInfo.ModifiedTime.HasValue
                    && googleFileInfo.ModifiedTime.Value.ToString() == gFile.ModifiedTime.Value.ToString()
#if NET45
                    && Alphaleonis.Win32.Filesystem.File.Exists(Path.Combine(DownloadsFolder, googleFileInfo.LocalPath)))
#else
                    && System.IO.File.Exists(Path.Combine(DownloadsFolder, googleFileInfo.LocalPath)))
#endif
                {
                    PullContentProgress.CurrentPullingStatus = CurrentPullingStatus.AlreadyExported;
                    ProgressChanged?.Invoke(PullContentProgress);
                    return;
                }
            }
            else if (!isGoogleType && !string.IsNullOrWhiteSpace(gFile.Md5Checksum))
            {
                if (gFile.Capabilities.CanDownload.HasValue && !gFile.Capabilities.CanDownload.Value)
                {
                    return;
                }
                GoogleFileInfo googleFileInfo = DbContext.GoogleFiles.Query().SingleOrDefault(x => x.GoogleId == gFile.Id);
                if (googleFileInfo != null
                    && !string.IsNullOrWhiteSpace(googleFileInfo.Md5Checksum)
                    && !string.IsNullOrWhiteSpace(googleFileInfo.LocalPath)
                    && gFile.Md5Checksum == googleFileInfo.Md5Checksum
#if NET45
                    && Alphaleonis.Win32.Filesystem.File.Exists(Path.Combine(DownloadsFolder, googleFileInfo.LocalPath)))
#else
                    && System.IO.File.Exists(Path.Combine(DownloadsFolder, googleFileInfo.LocalPath)))
#endif
                {
                    PullContentProgress.CurrentPullingStatus = CurrentPullingStatus.AlreadyDownloaded;
                    ProgressChanged?.Invoke(PullContentProgress);
                    return;
                }
            }

            string fileName = isGoogleType && MimeTypesConvertMap.ContainsKey(gFile.MimeType)
                ? gFile.Name + '.' + MimeTypesConvertMap[gFile.MimeType].FileExtension
                : gFile.Name;
            fileName = MakeValidFileName(fileName);

            string parentId = gFile.Parents?.FirstOrDefault();
            var parent = !string.IsNullOrEmpty(parentId) ? GetParent(parentId) : null;

            string filePath;
            if (parent != null)
            {
                filePath = Path.Combine(PrepareFolder(parent), fileName);
            }
            else
            {
                filePath = fileName;
            }

            DownloadingFile = gFile;

            if (isGoogleType)
            {
                // Only if we know what type we are converting to
                if (MimeTypesConvertMap.ContainsKey(gFile.MimeType))
                {
#if NET45
                    using (var file = Alphaleonis.Win32.Filesystem.File.Create(Path.Combine(DownloadsFolder, filePath)))
#else
                    using (FileStream file = new FileStream(Path.Combine(DownloadsFolder, filePath), System.IO.FileMode.Create, System.IO.FileAccess.Write))
#endif
                    {
                        ExecuteExport(file, filePath, gFile, MimeTypesConvertMap[gFile.MimeType].MimeType);
                    }
                }
                else
                {
                    PullContentProgress.CurrentPullingStatus = CurrentPullingStatus.SkippedExport;
                    ProgressChanged?.Invoke(PullContentProgress);
                }
            }
            else
            {
#if NET45
                using (var file = Alphaleonis.Win32.Filesystem.File.Create(Path.Combine(DownloadsFolder, filePath)))
#else
                using (var file = new FileStream(Path.Combine(DownloadsFolder, filePath), System.IO.FileMode.Create, System.IO.FileAccess.Write))
#endif
                {
                    ExecuteDownload(file, filePath, gFile);
                }
            }
        }

        private void ExecuteExport(System.IO.FileStream fileStream, string filePath, Google.Apis.Drive.v3.Data.File gFile, string mimeType)
        {
            PullContentProgress.CurrentPullingStatus = CurrentPullingStatus.Exporting;
            var request = FilesProvider.Export(gFile.Id, mimeType);
            request.MediaDownloader.ProgressChanged += Download_ProgressChanged;
            var downloadProgress = request.DownloadWithStatus(fileStream);
            var googleFileInfo = CorrectFileOnFS(fileStream, filePath, gFile, downloadProgress.Status);
            if (googleFileInfo != null)
            {
                googleFileInfo.InternalСategory = InternalСategory.Google;
                googleFileInfo.Name = Path.GetFileName(filePath);
                DbContext.GoogleFiles.Save(googleFileInfo);
            }
        }

        private void ExecuteDownload(System.IO.FileStream fileStream, string filePath, Google.Apis.Drive.v3.Data.File gFile)
        {
            PullContentProgress.CurrentPullingStatus = CurrentPullingStatus.Downloading;
            var request = FilesProvider.Get(gFile.Id);
            request.MediaDownloader.ProgressChanged += Download_ProgressChanged;
            var downloadProgress = request.DownloadWithStatus(fileStream);
            var googleFileInfo = CorrectFileOnFS(fileStream, filePath, gFile, downloadProgress.Status);
            if (googleFileInfo != null)
            {
                googleFileInfo.InternalСategory = InternalСategory.Binary;
                googleFileInfo.Md5Checksum = gFile.Md5Checksum;
                googleFileInfo.Name = gFile.OriginalFilename ?? gFile.Name;
                DbContext.GoogleFiles.Save(googleFileInfo);
            }
        }

        private GoogleFileInfo CorrectFileOnFS(System.IO.FileStream fileStream, string filePath, Google.Apis.Drive.v3.Data.File gFile, DownloadStatus downloadStatus)
        {
            string path = Path.Combine(DownloadsFolder, filePath);
            fileStream.Close();
            if (downloadStatus == DownloadStatus.Failed)
            {
                System.IO.File.Delete(path);
                return null;
            }
            else
            {
                FileInfo fileInfo = new FileInfo(path);
                if (gFile.CreatedTime.HasValue)
                {
                    fileInfo.CreationTime = gFile.CreatedTime.Value;
                }
                if (gFile.ModifiedTime.HasValue)
                {
                    fileInfo.LastWriteTime = gFile.ModifiedTime.Value;
                }
                GoogleFileInfo googleFileInfo = DbContext.GoogleFiles.Query().SingleOrDefault(x => x.GoogleId == gFile.Id);
                if (googleFileInfo == null)
                {
                    googleFileInfo = new GoogleFileInfo()
                    {
                        GoogleId = gFile.Id,
                        MimeType = gFile.MimeType,
                        CreatedTime = gFile.CreatedTime
                    };
                }
                googleFileInfo.ModifiedTime = gFile.ModifiedTime;
                googleFileInfo.LocalPath = filePath;
                return googleFileInfo;
            }
        }

        private string PrepareFolder(Google.Apis.Drive.v3.Data.File gFile)
        {
            // Check and add to cache
            if (!FilesCache.ContainsKey(gFile.Id))
            {
                FilesCache.Add(gFile.Id, gFile);
            }

            Stack<Google.Apis.Drive.v3.Data.File> gFileStack = new Stack<Google.Apis.Drive.v3.Data.File>();
            gFileStack.Push(gFile);
            FillStackByParents(gFile, gFileStack);

            return CreateDirectories(gFile, gFileStack); ;
        }

        private string CreateDirectories(Google.Apis.Drive.v3.Data.File gFile, Stack<Google.Apis.Drive.v3.Data.File> gFileStack)
        {
            string subPath = "";
            foreach (var item in gFileStack)
            {
                subPath = Path.Combine(subPath, MakeValidFileName(item.Name));
                CreateDirectory(item, Path.Combine(DownloadsFolder, subPath));
                GoogleFileInfo googleFileInfo = DbContext.GoogleFiles.Query().SingleOrDefault(x => x.GoogleId == item.Id);
                if (googleFileInfo == null)
                {
                    googleFileInfo = new GoogleFileInfo()
                    {
                        GoogleId = item.Id,
                        MimeType = item.MimeType,
                        CreatedTime = item.CreatedTime,
                        InternalСategory = InternalСategory.Folder,
                        LocalPath = subPath
                    };
                }
                googleFileInfo.ModifiedTime = item.ModifiedTime;
                googleFileInfo.Md5Checksum = item.Md5Checksum;
                googleFileInfo.Name = item.Name;
                DbContext.GoogleFiles.Save(googleFileInfo);
            }
            return subPath;
        }

        private void CreateDirectory(Google.Apis.Drive.v3.Data.File gFile, string resultFullPath)
        {
            if (!Directory.Exists(resultFullPath))
            {
#if NET45
                var directoryInfo = Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(resultFullPath);
#else
                var directoryInfo = Directory.CreateDirectory(resultFullPath);
#endif
                if (gFile.CreatedTime.HasValue)
                {
                    directoryInfo.CreationTime = gFile.CreatedTime.Value;
                }
            }
        }

        private void FillStackByParents(Google.Apis.Drive.v3.Data.File gFile, Stack<Google.Apis.Drive.v3.Data.File> gFileStack)
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

        private Google.Apis.Drive.v3.Data.File GetParent(string id)
        {
            // Check cache
            if (FilesCache.ContainsKey(id))
            {
                return FilesCache[id];
            }

            // Fetch file from drive
            var request = FilesProvider.Get(id);
            request.Fields = "id, name, createdTime, modifiedTime, mimeType, parents";
            var parent = request.Execute();

            // Save in cache
            FilesCache.Add(id, parent);

            return parent;
        }

        private string MakeValidFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return Regex.Replace(name, invalidRegStr, "_");
        }

        private void PreparyDirectories(bool isRemoveDirectories = false)
        {
            if (isRemoveDirectories && Directory.Exists(DownloadsFolder))
            {
                Directory.Delete(DownloadsFolder, true);
            }
            if (!Directory.Exists(DownloadsFolder))
            {
                Directory.CreateDirectory(DownloadsFolder);
            }
        }

        private void Download_ProgressChanged(IDownloadProgress progress)
        {
            PullContentProgress.CurrentItemDownloadProgress = progress;
            if (progress.Status == DownloadStatus.Completed)
            {
                PullContentProgress.CurrentPullingStatus = PullContentProgress.CurrentPullingStatus == CurrentPullingStatus.Downloading 
                    ? CurrentPullingStatus.Downloaded 
                    : CurrentPullingStatus.Exported;
            }
            else if (progress.Status == DownloadStatus.Failed)
            {
                PullContentProgress.CurrentPullingStatus = PullContentProgress.CurrentPullingStatus == CurrentPullingStatus.Downloading
                    ? CurrentPullingStatus.FailedDownload
                    : CurrentPullingStatus.FailedExport;
            }
            ProgressChanged?.Invoke(PullContentProgress);
        }
    }
}
