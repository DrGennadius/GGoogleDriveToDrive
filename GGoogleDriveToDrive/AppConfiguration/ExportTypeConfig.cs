namespace GGoogleDriveToDrive.AppConfiguration
{
    /// <summary>
    /// Export type config. Used to convert types from Google to another when exporting.
    /// </summary>
    public struct ExportTypeConfig
    {
        /// <summary>
        /// Mime type for exporting file.
        /// </summary>
        public string MimeType;

        /// <summary>
        /// File extension for exporting file.
        /// </summary>
        public string FileExtension;

        public ExportTypeConfig(string mimeType, string fileExtension)
        {
            MimeType = mimeType;
            FileExtension = fileExtension;
        }
    }
}
