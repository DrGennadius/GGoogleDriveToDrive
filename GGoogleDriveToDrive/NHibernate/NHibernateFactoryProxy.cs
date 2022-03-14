using NHibernate;
using NHibernate.Cfg;

namespace GGoogleDriveToDrive.NHibernate
{
    public class NHibernateFactoryProxy
    {
        public ISessionFactory SessionFactory { get; private set; }
        public Configuration Configuration { get; private set; }

        public void Initialize(Configuration configuration, ISessionFactory sessionFactory)
        {
            Configuration = configuration;
            SessionFactory = sessionFactory;
        }
    }
}
