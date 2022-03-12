namespace GGoogleDriveToDrive
{
    public struct ExportTypeConfig
    {
        public string MimeType;

        public string FileExtension;

        public ExportTypeConfig(string mimeType, string fileExtension)
        {
            MimeType = mimeType;
            FileExtension = fileExtension;
        }
    }
}
