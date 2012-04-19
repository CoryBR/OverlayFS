using System;
using System.Collections.Generic;
using System.Text;

namespace OverlayFS
{
    class Logger
    {
        long timerStart = 0;

        private System.IO.StreamWriter file;
        private MessagePriority priority = 0;
        private String logPath;
        public Logger(String logPath, MessagePriority logLevel)
        {
            this.logPath = logPath;
            //file = new System.IO.StreamWriter(logPath, true);
            RollLog();
            priority = logLevel;

            timerStart = DateTime.Now.Ticks;
            System.Timers.Timer LogRoller = new System.Timers.Timer();
            LogRoller.Elapsed += new System.Timers.ElapsedEventHandler(LogRoller_Elapsed);
            LogRoller.Interval = 10 * 60 * 60 * 1000; //Roll every 10 hours
            LogRoller.Enabled = true;
        }

        public void WriteLine(String value, MessagePriority messagePriority)
        {
            if (messagePriority <= priority && file != null)
            {
                String date = DateTime.Now.ToString();
                file.WriteLine(date + ": " + value);
                file.Flush();
            }
        }

        public enum MessagePriority
        {
            None = 0,
            Info = 1,
            UserInfo = 2,
            Debug = 3,
        }

        void LogRoller_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (DateTime.Now.Ticks > timerStart + 10000)
            {
                RollLog();
                timerStart = DateTime.Now.Ticks;
            }
        }

        public void RollLog()
        {
            if (file != null)
            {
                try
                {
                    file.Close();
                }
                catch
                {
                }
            }
            String oldLogPath = System.IO.Path.GetDirectoryName(logPath) + "\\" + System.IO.Path.GetFileNameWithoutExtension(logPath) + "-old" + System.IO.Path.GetExtension(logPath);
            if (System.IO.File.Exists(oldLogPath))
            {
                System.IO.File.Delete(oldLogPath);
            }
            if (System.IO.File.Exists(logPath))
            {
                System.IO.File.Move(logPath, oldLogPath);
            }
            file = new System.IO.StreamWriter(logPath, false);
        }
    }
}
