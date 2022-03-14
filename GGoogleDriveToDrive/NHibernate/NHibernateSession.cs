using NHibernate;

namespace GGoogleDriveToDrive.NHibernate
{
    /// <summary>
    /// NHibernate session.
    /// </summary>
    public class NHibernateSession
    {
        private readonly NHibernateFactoryProxy _nHibernateFactoryProxy;

        public NHibernateSession(NHibernateFactoryProxy nHibernateFactoryProxy)
        {
            _nHibernateFactoryProxy = nHibernateFactoryProxy;
        }

        public ISession OpenSession()
        {
            var session = _nHibernateFactoryProxy.SessionFactory.OpenSession();

            return session;
        }
    }
}
