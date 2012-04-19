using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using Dokan;

namespace OverlayFS
{
    public partial class Service1 : ServiceBase
    {
        private static System.Threading.ThreadStart t = new System.Threading.ThreadStart(StartThread);
        private static System.Threading.Thread mainThread = new System.Threading.Thread(t);
        private static List<String> mountedFS = new List<string>();

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            mainThread.Start();
        }

        protected override void OnStop()
        {
            foreach (String d in mountedFS){
                Dokan.DokanNet.DokanRemoveMountPoint(d);
            }
        }

        public static void StartThread()
        {
            //Needs EventLog work, exception thrown
            if (!EventLog.SourceExists("OverlayFS"))
            {
                EventLog.CreateEventSource("OverlayFS", "Application");
            }
            String appDirectory = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().Modules[0].FileName) + "\\";
            String settingsPath = appDirectory + "settings.xml";
            //Console.WriteLine("Loading settings from " + settingsPath);
            System.Xml.XmlDocument settingsXML = new System.Xml.XmlDocument();
            try
            {
                try
                {
                    settingsXML.Load(settingsPath);
                }
                catch (Exception)
                {
                    EventLog.WriteEntry("OverlayFS", "Error loading " + settingsPath, EventLogEntryType.Error);
                    throw;
                }


                foreach (System.Xml.XmlElement overlaySetting in settingsXML.DocumentElement)
                {
                    if (overlaySetting.Name.Equals("overlay"))
                    {
                        int logLevel;
                        if (!Int32.TryParse(overlaySetting.GetAttribute("logLevel"), out logLevel))
                        {
                            logLevel = (int)Logger.MessagePriority.Info;
                        }
                        OverlayFSMount ofsm = new OverlayFSMount();
                        ofsm.source = overlaySetting.GetAttribute("source");
                        ofsm.destination = overlaySetting.GetAttribute("destination");
                        ofsm.logLevel = logLevel;

                        System.Threading.ParameterizedThreadStart pts = new System.Threading.ParameterizedThreadStart(MountThread);
                        System.Threading.Thread t = new System.Threading.Thread(pts);
                        t.Start(ofsm);

                    }
                }
            }
            catch (Exception e)
            {
                //Console.Error.WriteLine(e.Message);
                EventLog.WriteEntry("OverlayFS", e.Message + "\n\n" + e.StackTrace, EventLogEntryType.Error);
                Console.ReadLine();
            }
        }

        private static void MountThread(Object ofsmP)
        {
            if (ofsmP is OverlayFSMount)
            {
                OverlayFSMount ofsm = (OverlayFSMount)ofsmP;
                String appDirectory = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().Modules[0].FileName) + "\\";
                DokanOptions opt = new DokanOptions();
                opt.DebugMode = true;
                opt.MountPoint = ofsm.destination;
                opt.ThreadCount = 5;
                opt.RemovableDrive = true;
                mountedFS.Add(ofsm.destination);
                int status = DokanNet.DokanMain(opt, new OverlayFS(ofsm.source, appDirectory + "log-" + System.IO.Path.GetFileName(ofsm.destination) + ".txt", ofsm.logLevel));
                switch (status)
                {
                    case DokanNet.DOKAN_DRIVE_LETTER_ERROR:
                        EventLog.WriteEntry("OverlayFS", "Drive letter error mounting " + ofsm.source + " to " + opt.MountPoint, EventLogEntryType.Error);
                        //Console.WriteLine("Drvie letter error");
                        break;
                    case DokanNet.DOKAN_DRIVER_INSTALL_ERROR:
                        //Console.WriteLine("Driver install error");
                        EventLog.WriteEntry("OverlayFS", "Driver install error mounting " + ofsm.source + " to " + opt.MountPoint, EventLogEntryType.Error);
                        break;
                    case DokanNet.DOKAN_MOUNT_ERROR:
                        //Console.WriteLine("Mount error");
                        EventLog.WriteEntry("OverlayFS", "Mount error mounting " + ofsm.source + " to " + opt.MountPoint, EventLogEntryType.Error);
                        break;
                    case DokanNet.DOKAN_START_ERROR:
                        //Console.WriteLine("Start error");
                        EventLog.WriteEntry("OverlayFS", "Start error mounting " + ofsm.source + " to " + opt.MountPoint, EventLogEntryType.Error);
                        break;
                    case DokanNet.DOKAN_ERROR:
                        //Console.WriteLine("Unknown error");
                        EventLog.WriteEntry("OverlayFS", "Unknown error mounting " + ofsm.source + " to " + opt.MountPoint, EventLogEntryType.Error);
                        break;
                    case DokanNet.DOKAN_SUCCESS:
                        //Console.WriteLine("Success");
                        break;
                    default:
                        //Console.WriteLine("Unknown status: " + status);
                        EventLog.WriteEntry("OverlayFS", "Unknown status: " + status + " mounting " + ofsm.source + " to " + opt.MountPoint, EventLogEntryType.Error);
                        break;
                }
            }
        }

        private struct OverlayFSMount
        {
            public String source;
            public String destination;
            public int logLevel;
        }
    }
}
