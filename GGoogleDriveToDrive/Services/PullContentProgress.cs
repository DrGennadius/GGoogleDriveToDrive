using Google.Apis.Download;
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

        public Exception Exception { get; set; }
    }

    public enum PullContentProgressStatus
    {
        None,
        Processing,
        Completed,
        Failed
    }
}
