using Google.Apis.Download;
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
            Logging("Start command.");
            Init();
            Processing();
            Console.Read();
        }

        static void Init()
        {
            GoogleDriveManager.Initialize();
            GoogleDriveManager.ProgressChanged += GoogleDriveManager_ProgressChanged;
        }

        static void Processing()
        {
            Console.WriteLine("Processing...");
            Logging("Processing...");
            var pullContentProgress = GoogleDriveManager.PullContent();
            FinishPrint(pullContentProgress);
        }

        static void FinishPrint(PullContentProgress pullContentProgress)
        {
            Console.WriteLine("Done!");
            Logging("Done!");
            Console.WriteLine($"Status {pullContentProgress.Status}.");
            Logging($"Status {pullContentProgress.Status}.");
            Console.WriteLine($"Processed {pullContentProgress.ItemsCount} items.");
            Logging($"Processed {pullContentProgress.ItemsCount} items.");
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

        static void GoogleDriveManager_ProgressChanged(PullContentProgress pullContentProgress)
        {
            switch (pullContentProgress.Status)
            {
                case PullContentProgressStatus.None:
                    break;
                case PullContentProgressStatus.Processing:
                    break;
                case PullContentProgressStatus.Completed:
                    Console.WriteLine($"{pullContentProgress.Status} items: {pullContentProgress.ItemsCount}.");
                    break;
                case PullContentProgressStatus.Failed:
                    Console.WriteLine(pullContentProgress.Status);
                    Console.WriteLine(pullContentProgress.Exception);
                    break;
                default:
                    break;
            }
            var currentDownloadProgress = pullContentProgress.CurrentItemDownloadProgress;
            if (currentDownloadProgress != null)
            {
                switch (currentDownloadProgress.Status)
                {
                    case DownloadStatus.NotStarted:
                        break;
                    case DownloadStatus.Downloading:
                        Console.WriteLine($"[{pullContentProgress.ItemsCount}] Downloading: {currentDownloadProgress.Status} {currentDownloadProgress.BytesDownloaded} bytes.");
                        break;
                    case DownloadStatus.Completed:
                        Console.WriteLine($"[{pullContentProgress.ItemsCount}] Downloading: {currentDownloadProgress.Status} {currentDownloadProgress.BytesDownloaded} bytes.");
                        break;
                    case DownloadStatus.Failed:
                        Console.WriteLine($"[{pullContentProgress.ItemsCount}] Downloading: {currentDownloadProgress.Status}");
                        Console.WriteLine(currentDownloadProgress.Exception);
                        break;
                    default:
                        break;
                }
            }
        }

        static void Logging(string message)
        {
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(LoggingFileName, true))
            {
                writer.WriteLine($"{DateTime.Now} {message}");
            }
        }
    }
}
