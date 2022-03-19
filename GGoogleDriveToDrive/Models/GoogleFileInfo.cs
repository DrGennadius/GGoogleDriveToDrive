using System;

namespace GGoogleDriveToDrive.Models
{
    /// <summary>
    /// Google file info.
    /// </summary>
    public class GoogleFileInfo : IEntity
    {
        public GoogleFileInfo()
        {
            InternalСategory = InternalСategory.Unknown;
        }

        /// <summary>
        /// ID.
        /// </summary>
        public virtual long Id { get; set; }

        /// <summary>
        /// Google ID.
        /// </summary>
        public virtual string GoogleId { get; set; }

        /// <summary>
        /// Name.
        /// </summary>
        public virtual string Name { get; set; }

        /// <summary>
        /// Mime type.
        /// </summary>
        public virtual string MimeType { get; set; }

        /// <summary>
        /// MD5 Checksum.
        /// </summary>
        public virtual string Md5Checksum { get; set; }

        /// <summary>
        /// Created time.
        /// </summary>
        public virtual DateTime? CreatedTime { get; set; }

        /// <summary>
        /// Modified time.
        /// </summary>
        public virtual DateTime? ModifiedTime { get; set; }

        /// <summary>
        /// Internal category.
        /// </summary>
        public virtual InternalСategory InternalСategory { get; set; }

        /// <summary>
        /// Local path relative to download folder.
        /// </summary>
        public virtual string LocalPath { get; set; }
    }

    public enum InternalСategory
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Google special format (gdoc, etc). Using export to binary format.
        /// </summary>
        Google,

        /// <summary>
        /// Binary type. It can be downloaded as a file directly without conversion.
        /// </summary>
        Binary,

        /// <summary>
        /// Folder.
        /// </summary>
        Folder
    }
}
