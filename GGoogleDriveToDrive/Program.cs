using System;
using GGoogleDriveToDrive.Services;

#if NET45
using Alphaleonis.Win32.Filesystem;
#else
using System.IO;
#endif

namespace GGoogleDriveToDrive
{
    class Program
    {
        const string LoggingFileName = "logging.txt";
        static readonly GoogleDriveManager GoogleDriveManager = new GoogleDriveManager();

        static void Main(string[] args)
        {
            Logging($"{Environment.NewLine}Start command.");
            if (args.Length == 1)
            {
                Init(args[0]);
            }
            else
            {
                Init();
            }
            Processing();
            Console.Read();
        }

        private static void Init(string downloadsDirectory = "")
        {
            GoogleDriveManager.Initialize(downloadsDirectory);
            GoogleDriveManager.ProgressChanged += GoogleDriveManager_ProgressChanged;
        }

        private static void Processing()
        {
            WriteLine("Processing...");
            var pullContentProgress = GoogleDriveManager.PullContent();
            FinishPrint(pullContentProgress);
        }

        private static void FinishPrint(PullContentProgress pullContentProgress)
        {
            WriteLine("Done!");
            WriteLine($"Status {pullContentProgress.Status}.");
            WriteLine($"Processed {pullContentProgress.ItemsCount} items.");
        }

        private static void GoogleDriveManager_ProgressChanged(PullContentProgress pullContentProgress)
        {
            switch (pullContentProgress.Status)
            {
                case PullContentProgressStatus.None:
                    break;
                case PullContentProgressStatus.Processing:
                    PrintCurrentItemProgress(pullContentProgress);
                    break;
                case PullContentProgressStatus.Completed:
                    WriteLine($"{pullContentProgress.Status} items: {pullContentProgress.ItemsCount}.");
                    break;
                case PullContentProgressStatus.Failed:
                    WriteLine(pullContentProgress.Status);
                    WriteLine(pullContentProgress.Exception);
                    break;
                default:
                    break;
            }
        }

        private static void PrintCurrentItemProgress(PullContentProgress pullContentProgress)
        {
            string currentGFileNamePart = pullContentProgress.CurrentGoogleFile?.Name;
            if (!string.IsNullOrEmpty(currentGFileNamePart))
            {
                currentGFileNamePart = " file: '" + currentGFileNamePart + '\'';
            }
            var currentDownloadProgress = pullContentProgress.CurrentItemDownloadProgress;
            if (pullContentProgress.CurrentPullingStatus == CurrentPullingStatus.FailedDownload
                || pullContentProgress.CurrentPullingStatus == CurrentPullingStatus.FailedExport)
            {
                WriteLine($"[{pullContentProgress.ItemsCount}] {pullContentProgress.CurrentPullingStatus}{currentGFileNamePart}");
                var exception = currentDownloadProgress?.Exception;
                if (exception != null)
                {
                    WriteLine(exception);
                }
                else
                {
                    WriteLine("An undefined error occurred.");
                }
            }
            else
            {
                string bytesDownloadedString = currentDownloadProgress != null
                    ? $" {currentDownloadProgress.BytesDownloaded} bytes" : "";
                WriteLine($"[{pullContentProgress.ItemsCount}] {pullContentProgress.CurrentPullingStatus}{bytesDownloadedString}{currentGFileNamePart}");
            }
        }

        private static void WriteLine(object value)
        {
            Logging(value.ToString());
            Console.WriteLine(value);
        }

        private static void WriteLine(string value)
        {
            Logging(value);
            Console.WriteLine(value);
        }

        private static void Logging(string message)
        {
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(LoggingFileName, true))
            {
                writer.WriteLine($"{DateTime.Now} {message}");
            }
        }
    }
}
