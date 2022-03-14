using GGoogleDriveToDrive.Models;
using GGoogleDriveToDrive.NHibernate;
using NHibernate;
using System;

namespace GGoogleDriveToDrive.DataBase
{
    /// <summary>
    /// Database Context (UoW)
    /// </summary>
    public class DbContext : IDbContext
    {
        private readonly string _dataBaseFilePath = "database.db";

        public DbContext(string dataBaseFilePath)
        {
            _dataBaseFilePath = dataBaseFilePath;
            Initialize();
        }

        public DbContext()
        {
            Initialize();
        }

        public ISession Session { get; private set; }

        public EntityRepository<GoogleFileInfo> GoogleFiles { get; private set; }

        public ITransaction BeginTransaction()
        {
            return Session.BeginTransaction();
        }

        public void Commit()
        {
#if NET45
            var transaction = Session.Transaction;
#else
            var transaction = Session.GetCurrentTransaction();
#endif
            if (transaction != null && transaction.IsActive)
            {
                transaction.Commit();
            }
        }

        public void Dispose()
        {
            Session.Dispose();
            GC.SuppressFinalize(this);
        }

        public (ITransaction, bool) GetCurrentTransactionOrCreateNew()
        {
#if NET45
            var transaction = Session.Transaction;
#else
            var transaction = Session.GetCurrentTransaction();
#endif
            bool isNew = transaction == null || !transaction.IsActive;
            if (isNew)
            {
                transaction = Session.BeginTransaction();
            }
            return (transaction, isNew); 
        }

        public void ResetSession()
        {
            Session.Disconnect();
            Session.Clear();
            Session.Reconnect();
        }

        public void Rollback()
        {
#if NET45
            var transaction = Session.Transaction;
#else
            var transaction = Session.GetCurrentTransaction();
#endif
            if (transaction != null && transaction.IsActive)
            {
                transaction.Rollback();
            }
        }

        private void Initialize()
        {
            var nHibernateFactory = new NHibernateFactory(_dataBaseFilePath);
            var nHibernateFactoryProxy = new NHibernateFactoryProxy();
            nHibernateFactoryProxy.Initialize(nHibernateFactory.Configuration, nHibernateFactory.SessionFactory);
            var nHibernateSession = new NHibernateSession(nHibernateFactoryProxy);
            Session = nHibernateSession.OpenSession();
            Session.FlushMode = FlushMode.Auto;

            GoogleFiles = new EntityRepository<GoogleFileInfo>(this);
        }
    }
}
