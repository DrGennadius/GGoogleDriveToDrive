using FluentNHibernate.Mapping;
using GGoogleDriveToDrive.Models;

namespace GGoogleDriveToDrive.Mapping
{
    public class GoogleFileInfoMap : ClassMap<GoogleFileInfo>
    {
        public GoogleFileInfoMap()
        {
            Id(x => x.Id)
                .GeneratedBy.Increment();
            Map(x => x.GoogleId)
                .Not.Nullable();
            Map(x => x.Name);
            Map(x => x.MimeType);
            Map(x => x.Md5Checksum);
            Map(x => x.CreatedTime);
            Map(x => x.ModifiedTime);
            Map(x => x.InternalСategory);
            Map(x => x.LocalPath);
        }
    }
}
