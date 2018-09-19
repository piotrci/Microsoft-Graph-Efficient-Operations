using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfficientGraphOperations
{
    public static class Logger
    {
        public static void SetLogger(ILogger logger)
        {
            logWriter = logger;
        }

        public static void FlushAndCloseLogs()
        {
            logWriter.Dispose();
        }

        public static void WriteLine(string line)
        {
            logWriter.WriteLine(line);
        }

        private static ILogger logWriter = new DummyLogger();

        private class DummyLogger : ILogger, IDisposable
        {
            public void Dispose()
            {
                return;
            }

            public void WriteLine(string line)
            {
                return;
            }
        }
    }

    public interface ILogger : IDisposable
    {
        void WriteLine(string line);
    }

    
}
