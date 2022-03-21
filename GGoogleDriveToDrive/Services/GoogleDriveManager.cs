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
        private readonly Dictionary<string, Google.Apis.Drive.v3.Data.File> GoogleFilesCache = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();
        private readonly DbContext DbContext = new DbContext();
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

        public AppConfig AppConfiguration { get; private set; }

        public Dictionary<string, ExportTypeConfig> MimeTypesConvertMap { get; private set; }

        public event Action<PullContentProgress> ProgressChanged;

        /// <summary>
        /// Initialize. Prepare directories, read config, authorization and init service.
        /// </summary>
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
                PrepareWorkDirectories(isEmptyAuth);
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

        /// <summary>
        /// Process of pulling content to local drive.
        /// </summary>
        /// <returns></returns>
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
                AdjustCacheDirectories();
                PullContentProgress.Status = PullContentProgressStatus.Completed;
            }
            catch (Exception ex)
            {
                PullContentProgress.Status = PullContentProgressStatus.Failed;
                PullContentProgress.Exception = ex;
            }

            return PullContentProgress;
        }

        /// <summary>
        /// Process of pulling content to local drive for particular item (Goodle Drive file).
        /// </summary>
        /// <param name="gFile"></param>
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
            bool isSkipped = CheckItemForSkip(gFile, isGoogleType);
            if (isSkipped)
            {
                return;
            }

            string fileName = isGoogleType && MimeTypesConvertMap.ContainsKey(gFile.MimeType)
                ? gFile.Name + '.' + MimeTypesConvertMap[gFile.MimeType].FileExtension
                : gFile.Name;
            fileName = MakeValidPathName(fileName);
            var parent = GetParent(gFile);

            string filePath;
            if (parent != null)
            {
                filePath = Path.Combine(PrepareFolder(parent), fileName);
            }
            else
            {
                filePath = fileName;
            }

            PullContentToDrive(gFile, isGoogleType, filePath);
        }

        /// <summary>
        /// Checking if content is needed for pulling.
        /// </summary>
        /// <param name="gFile">Google item.</param>
        /// <param name="isGoogleType">Is Google type of item.</param>
        /// <returns></returns>
        private bool CheckItemForSkip(Google.Apis.Drive.v3.Data.File gFile, bool isGoogleType)
        {
            bool isSkipped = false;

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
                    isSkipped = true;
                }
            }
            else if (!isGoogleType && !string.IsNullOrWhiteSpace(gFile.Md5Checksum))
            {
                if (gFile.Capabilities.CanDownload.HasValue && !gFile.Capabilities.CanDownload.Value)
                {
                    isSkipped = true;
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
                    isSkipped = true;
                }
            }

            return isSkipped;
        }

        /// <summary>
        /// Pull (download or export) Google Drive item to local drive.
        /// </summary>
        /// <param name="gFile">Google item.</param>
        /// <param name="isGoogleType">Is Google type of item.</param>
        /// <param name="filePath">Local file path.</param>
        private void PullContentToDrive(Google.Apis.Drive.v3.Data.File gFile, bool isGoogleType, string filePath)
        {
#if NET45
            using (var file = Alphaleonis.Win32.Filesystem.File.Create(Path.Combine(DownloadsFolder, filePath)))
#else
            using (var file = new FileStream(Path.Combine(DownloadsFolder, filePath), System.IO.FileMode.Create, System.IO.FileAccess.Write))
#endif
            {
                if (isGoogleType)
                {
                    // Only if we know what type we are converting to
                    if (MimeTypesConvertMap.ContainsKey(gFile.MimeType))
                    {
                        ExecuteExport(file, filePath, gFile, MimeTypesConvertMap[gFile.MimeType].MimeType);
                    }
                    else
                    {
                        PullContentProgress.CurrentPullingStatus = CurrentPullingStatus.SkippedExport;
                        ProgressChanged?.Invoke(PullContentProgress);
                    }
                }
                else
                {
                    ExecuteDownload(file, filePath, gFile);
                }
            }
        }

        /// <summary>
        /// Export item from Google drive to local drive as binary file using converting.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="filePath"></param>
        /// <param name="gFile"></param>
        /// <param name="mimeType"></param>
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

        /// <summary>
        /// Export binary file from Google drive to local drive.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="filePath"></param>
        /// <param name="gFile"></param>
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

        /// <summary>
        /// Correcting file and their info on the local file system after pulling.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="filePath"></param>
        /// <param name="gFile"></param>
        /// <param name="downloadStatus"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Prepare Google folder as a full directory in the local file system.
        /// </summary>
        /// <param name="gFile"></param>
        /// <returns></returns>
        private string PrepareFolder(Google.Apis.Drive.v3.Data.File gFile)
        {
            // Check and add to cache
            if (!GoogleFilesCache.ContainsKey(gFile.Id))
            {
                GoogleFilesCache.Add(gFile.Id, gFile);
            }
            var gFileStack = GetStackWithParents(gFile);
            return CreateDirectories(gFile, gFileStack); ;
        }

        /// <summary>
        /// Create the directories in the local file system by the Google folders.
        /// </summary>
        /// <param name="gFile"></param>
        /// <param name="gFileStack"></param>
        /// <returns></returns>
        private string CreateDirectories(Google.Apis.Drive.v3.Data.File gFile, Stack<Google.Apis.Drive.v3.Data.File> gFileStack)
        {
            string subPath = "";
            foreach (var item in gFileStack)
            {
                subPath = Path.Combine(subPath, MakeValidPathName(item.Name));
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

        /// <summary>
        /// Create the directory in the local file system by the Google folder item.
        /// </summary>
        /// <param name="gFile"></param>
        /// <param name="resultFullPath"></param>
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

        /// <summary>
        /// Get stack with parents.
        /// </summary>
        /// <param name="gFile"></param>
        /// <returns></returns>
        private Stack<Google.Apis.Drive.v3.Data.File> GetStackWithParents(Google.Apis.Drive.v3.Data.File gFile)
        {
            Stack<Google.Apis.Drive.v3.Data.File> gFilesStack = new Stack<Google.Apis.Drive.v3.Data.File>();
            var parent = gFile;
            while (parent != null)
            {
                gFilesStack.Push(parent);
                parent = GetParent(parent);
            }
            return gFilesStack;
        }

        /// <summary>
        /// Get parent of the item.
        /// </summary>
        /// <param name="gFile">Item.</param>
        /// <returns></returns>
        private Google.Apis.Drive.v3.Data.File GetParent(Google.Apis.Drive.v3.Data.File gFile)
        {
            string parentId = gFile.Parents?.FirstOrDefault();
            if (!string.IsNullOrEmpty(parentId))
            {
                // Check cache
                return GoogleFilesCache.ContainsKey(parentId)
                    ? GoogleFilesCache[parentId]
                    : GetItem(parentId);
            }
            return null;
        }

        /// <summary>
        /// Get item (Google Drive file).
        /// </summary>
        /// <param name="id">Item Id (Google Drive file id).</param>
        /// <returns></returns>
        private Google.Apis.Drive.v3.Data.File GetItem(string id)
        {
            // Fetch file from drive
            var request = FilesProvider.Get(id);
            request.Fields = "id, name, createdTime, modifiedTime, mimeType, parents";
            var gFile = request.Execute();

            // Save in cache
            GoogleFilesCache.Add(id, gFile);

            return gFile;
        }

        /// <summary>
        /// Get valid path name. Replace invalid chars to '_'.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string MakeValidPathName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return Regex.Replace(name, invalidRegStr, "_");
        }

        /// <summary>
        /// Prepare working directories.
        /// </summary>
        /// <param name="isRemoveDirectories">Is remove directories. If true - recurcive delete directory.</param>
        private void PrepareWorkDirectories(bool isRemoveDirectories = false)
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

        /// <summary>
        /// Adjust cache directories - change LastWriteTime by ModifiedTime from Google Drive.
        /// </summary>
        private void AdjustCacheDirectories()
        {
            var folders = GoogleFilesCache.Select(x => x.Value)
                                          .Where(x => x.MimeType == "application/vnd.google-apps.folder")
                                          .ToArray();

            var myDrive = folders.FirstOrDefault(x => x.Name == "My Drive");
            if (myDrive != null)
            {
                AdjustSubDirectories(folders, myDrive);
            }
        }

        /// <summary>
        /// Adjust cache directories - change LastWriteTime by ModifiedTime from Google Drive (recursive).
        /// </summary>
        /// <param name="folders"></param>
        /// <param name="parent"></param>
        private void AdjustSubDirectories(Google.Apis.Drive.v3.Data.File[] folders, Google.Apis.Drive.v3.Data.File parent)
        {
            var children = folders.Where(x => x.Parents != null && x.Parents.FirstOrDefault() == parent.Id).ToArray();
            foreach (var child in children)
            {
                AdjustSubDirectories(folders, child);
            }
            AdjustDirectory(parent);
        }

        /// <summary>
        /// Change LastWriteTime by ModifiedTime from Google Drive for the directory.
        /// </summary>
        /// <param name="folder"></param>
        private void AdjustDirectory(Google.Apis.Drive.v3.Data.File folder)
        {
            if (folder.ModifiedTime.HasValue)
            {
                GoogleFileInfo googleFileInfo = DbContext.GoogleFiles.Query().SingleOrDefault(x => x.GoogleId == folder.Id);
                if (googleFileInfo != null && !string.IsNullOrEmpty(googleFileInfo.LocalPath))
                {
                    string path = Path.Combine(DownloadsFolder, googleFileInfo.LocalPath);
                    if (Directory.Exists(path))
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo(path);
                        if (directoryInfo != null)
                        {
                            directoryInfo.LastWriteTime = folder.ModifiedTime.Value;
                        }
                    }
                }
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
