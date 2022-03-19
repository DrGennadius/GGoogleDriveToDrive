using Google.Apis.Download;
using Google.Apis.Drive.v3.Data;
using System;

namespace GGoogleDriveToDrive.Services
{
    /// <summary>
    /// Progress for operation of pulling (download and export) of files from Google Drive to local drive.
    /// </summary>
    public class PullContentProgress
    {
        public PullContentProgress()
        {
            Status = PullContentProgressStatus.None;
        }

        /// <summary>
        /// Item counter for the operation.
        /// </summary>
        public int ItemsCount { get; set; }

        /// <summary>
        /// Status of the operation.
        /// </summary>
        public PullContentProgressStatus Status { get; set; }

        /// <summary>
        /// Download progress for current item. Using Google Api scope.
        /// </summary>
        public IDownloadProgress CurrentItemDownloadProgress { get; set; }

        /// <summary>
        /// The current Google file at the time of the operation.
        /// </summary>
        public File CurrentGoogleFile { get; set; }

        /// <summary>
        /// The current pull state of a particular item (Google file).
        /// </summary>
        public CurrentPullingStatus CurrentPullingStatus { get; set; }

        /// <summary>
        /// The last exception.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Init new pulling for item (Google file).
        /// </summary>
        /// <param name="gFile">Item (Google file)</param>
        internal void InitNewPulling(File gFile)
        {
            CurrentGoogleFile = gFile;
            CurrentPullingStatus = CurrentPullingStatus.None;
            CurrentItemDownloadProgress = null;
            Exception = null;
        }
    }

    /// <summary>
    /// The status of the pull operation for all content.
    /// </summary>
    public enum PullContentProgressStatus
    {
        None,
        Processing,
        Completed,
        Failed
    }

    /// <summary>
    /// The current pull state of a particular item.
    /// </summary>
    public enum CurrentPullingStatus
    {
        None,
        Exporting,
        Downloading,
        PreparingFolder,
        Exported,
        Downloaded,
        PreparedFolder,
        SkippedExport,
        AlreadyExported,
        AlreadyDownloaded,
        AlreadyPreparedFolder,
        FailedExport,
        FailedDownload
    }
}
