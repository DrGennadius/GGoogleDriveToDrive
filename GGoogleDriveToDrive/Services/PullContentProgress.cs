using Google.Apis.Download;
using Google.Apis.Drive.v3.Data;
using System;

namespace GGoogleDriveToDrive.Services
{
    public class PullContentProgress
    {
        public PullContentProgress()
        {
            Status = PullContentProgressStatus.None;
        }

        public int ItemsCount { get; set; }

        public PullContentProgressStatus Status { get; set; }

        public IDownloadProgress CurrentItemDownloadProgress { get; set; }

        public File CurrentGoogleFile { get; set; }

        public CurrentPullingStatus CurrentPullingStatus { get; set; }

        public Exception Exception { get; set; }

        internal void InitNewPulling(File gFile)
        {
            CurrentGoogleFile = gFile;
            CurrentPullingStatus = CurrentPullingStatus.None;
            CurrentItemDownloadProgress = null;
            Exception = null;
        }
    }

    public enum PullContentProgressStatus
    {
        None,
        Processing,
        Completed,
        Failed
    }

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
