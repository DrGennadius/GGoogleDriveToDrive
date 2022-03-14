using GGoogleDriveToDrive.Models;
using NHibernate.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace GGoogleDriveToDrive.DataBase
{
    /// <summary>
    /// Repository of <see cref="IEntity"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EntityRepository<T> : IRepository<T> where T : IEntity
    {
        public EntityRepository(IDbContext dbContext)
        {
            DbContext = dbContext;
        }

        /// <summary>
        /// Database context.
        /// </summary>
        public IDbContext DbContext { get; private set; }

        public void Delete(T item)
        {
            var (transaction, isNew) = DbContext.GetCurrentTransactionOrCreateNew();
            DbContext.Session.Delete(item);
            if (isNew)
            {
                transaction.Commit();
            }
        }

        public IList<T> Find(Expression<Func<T, bool>> predicate)
        {
            return Query().Where(predicate).ToList();
        }

        public IList<T> GetAll()
        {
            return new List<T>(DbContext.Session.CreateCriteria(typeof(T)).List<T>());
        }

        public T GetById(long id)
        {
            return DbContext.Session.Get<T>(id);
        }

        public IQueryable<T> Query()
        {
            return DbContext.Session.Query<T>();
        }

        public long Save(T item)
        {
            var (transaction, isNew) = DbContext.GetCurrentTransactionOrCreateNew();
            long id = (long)DbContext.Session.Save(item);
            if (isNew)
            {
                transaction.Commit();
            }
            return id;
        }
    }
}
