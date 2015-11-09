using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Twitch2Steam
{
    static class LogTester
    {
        public static void Test()
        {
            ILog log = LogManager.GetLogger(typeof(LogTester));
            log.Fatal("Log Test Fatal");
            log.Warn("Log Test Warn");
            log.Error("Log Test Error");
            log.Info("Log Test Info");
            log.Debug("Log Test Debug");
        }
    }

}
