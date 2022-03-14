using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using GGoogleDriveToDrive.Models;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Tool.hbm2ddl;
using System;
using System.IO;

namespace GGoogleDriveToDrive.NHibernate
{
    public class NHibernateFactory
    {
        private readonly Lazy<Configuration> _configuration;
        private readonly Lazy<ISessionFactory> _sessionFactory;

        /// <summary>
        /// NHibernate Configuration.
        /// </summary>
        public Configuration Configuration => _configuration.Value;

        public ISessionFactory SessionFactory => _sessionFactory.Value;

        public NHibernateFactory(string dataBaseFilePath)
        {
            _configuration = new Lazy<Configuration>(() => Configure(dataBaseFilePath));
            _sessionFactory = new Lazy<ISessionFactory>(() => _configuration.Value.BuildSessionFactory());
        }

        private Configuration Configure(string dataBaseFilePath)
        {
            bool dbExists = File.Exists(dataBaseFilePath);
            return Fluently.Configure()
                .Database(
                    SQLiteConfiguration.Standard
                    .UsingFile(dataBaseFilePath)
                )
                .Mappings(m => m.FluentMappings.AddFromAssemblyOf<GoogleFileInfo>())
                .ExposeConfiguration(cfg => new SchemaExport(cfg).Create(false, !dbExists))
                .BuildConfiguration();
        }
    }
}
