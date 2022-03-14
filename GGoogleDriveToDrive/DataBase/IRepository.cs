using GGoogleDriveToDrive.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace GGoogleDriveToDrive.DataBase
{
    /// <summary>
    /// Repository of <see cref="IEntity"/>.
    /// </summary>
    /// <typeparam name="T">Entity</typeparam>
    public interface IRepository<T> where T : IEntity
    {
        /// <summary>
        /// Database context.
        /// </summary>
        IDbContext DbContext { get; }

        /// <summary>
        /// Save the entry to db. Return id.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        long Save(T item);

        /// <summary>
        /// Get item by ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        T GetById(long id);

        /// <summary>
        /// Retrieve all entries. Dont recommended for use in real cases.
        /// </summary>
        /// <returns></returns>
        IList<T> GetAll();

        /// <summary>
        /// Get Queryable.
        /// </summary>
        /// <returns></returns>
        IQueryable<T> Query();

        /// <summary>
        /// Find by expression.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        IList<T> Find(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Delete the item.
        /// </summary>
        /// <param name="item"></param>
        void Delete(T item);
    }
}
