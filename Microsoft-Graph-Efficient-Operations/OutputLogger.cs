using EfficientRequestHandling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MicrosoftGraphEfficientPatterns
{
    public class OutputLogger : IDisposable, ILogger
    {
        public OutputLogger(params Stream[] outputStreams)
        {
            if (outputStreams == null || outputStreams.Length < 1)
            {
                throw new ArgumentException("At least one output stream is needed", nameof(outputStreams));
            }
            this.writers = outputStreams.Select(o => new StreamWriter(o, Encoding.UTF8)).ToArray();
            // we want writers to flush aggressively so we can monitor progress
            foreach (var writer in this.writers)
            {
                writer.AutoFlush = true;
            }
            this.pump = Task.Factory.StartNew((Action)this.PumpBufferToStream, TaskCreationOptions.LongRunning);
        }
        public OutputLogger(string filePath) : this(File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)) { }
        
        private readonly StreamWriter[] writers;
        private readonly Task pump;
        private readonly BlockingCollection<string> logBuffer = new BlockingCollection<string>();

        public void LogLine(string logLine, params object[] items)
        {
            this.logBuffer.Add(DateTime.Now.ToString("s") + ": " + String.Format(logLine, items));
        }

        private void PumpBufferToStream()
        {
            foreach (var line in logBuffer.GetConsumingEnumerable())
            {
                foreach (var writer in this.writers)
                {
                    writer.WriteLine(line);
                }
            }
        }

        public void LogLines(IEnumerable<string> logLines)
        {
            foreach (var line in logLines)
            {
                this.LogLine(line);
            }
        }
        public void Dispose()
        {
            // mark the buffer as done
            this.logBuffer.CompleteAdding();
            // wait for the buffer to be fully flushed
            this.pump.Wait();
            // flush the underlying stream
            foreach (var writer in this.writers)
            {
                writer.Flush();
                writer.Dispose();
            }
        }

        public void WriteLine(string line)
        {
            this.LogLine(line);
        }
    }
}
