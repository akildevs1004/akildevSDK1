using log4net.Config;
using log4net.Repository;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Common
{
    public class LogHelper
    {
        // private static ILoggerRepository repository { get; set; }
        private static ILog log;
      

        static LogHelper()
        {
            string repositoryName = "RollingLogFileAppender";
            string configFile = "log4net.config";
            ILoggerRepository repository = LogManager.CreateRepository(repositoryName);
            XmlConfigurator.Configure(repository, new FileInfo(configFile));
            log = LogManager.GetLogger(repositoryName, "");
        }



        public static void Info(string msg)
        {
            log.Info(msg + Environment.NewLine);
        }
        public static void Info(string msg, Exception ex)
        {
            log.Info(msg + Environment.NewLine, ex);
        }
        public static void Info(object obj)
        {
            log.Info(obj);

        }
        public static void Warn(string msg)
        {
            log.Warn(msg + Environment.NewLine);
        }

        public static void Error(string msg)
        {
            log.Error(msg + Environment.NewLine);
        }
        public static void Error(string msg, Exception ex)
        {
            log.Error(msg + Environment.NewLine, ex);
        }
    }
}
