using System;
using System.IO;
using System.Collections;
using Dokan;
using OverlayFS;
using System.Management;
using System.Collections.Generic;

namespace OverlayFS
{
    class OverlayFS : DokanOperations
    {
        private Logger logger;
        private System.Object userFSLock = new System.Object();

        private Dictionary<String, UserFS> userFSList = new System.Collections.Generic.Dictionary<string, UserFS>();
        private Dictionary<int, String> userProcList = new Dictionary<int, string>();

        private string root_;
        private int count_;
        public OverlayFS(string root, string logPath, int logLevel)
        {
            logger = new Logger(logPath, (Logger.MessagePriority)logLevel);
            logger.WriteLine("OverlayFS mounted for " + root, Logger.MessagePriority.Info);

            root_ = root;
            count_ = 1;

            System.Timers.Timer UserFSGC = new System.Timers.Timer();
            UserFSGC.Elapsed += new System.Timers.ElapsedEventHandler(UserFSGC_Elapsed);
            UserFSGC.Interval = 60000;
            UserFSGC.Enabled = true;

            System.Timers.Timer UserFSStats = new System.Timers.Timer();
            UserFSStats.Elapsed += new System.Timers.ElapsedEventHandler(UserFSStats_Elapsed);
            UserFSStats.Interval = 1800000;
            UserFSStats.Enabled = true;

        }

        void UserFSGC_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            UserFSCleaner();
            ProcessListCleaner();
        }

        void UserFSStats_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            UserFSStats();
        }


        private void UserFSStats()
        {
            foreach (KeyValuePair<String, UserFS> var in userFSList)
            {
                Int64 ramUsage = 0;
                foreach (RamFile fi in var.Value.files)
                {
                    ramUsage += fi.GetRamUsage();
                }
                logger.WriteLine("Byte usage of " + var.Key + ": " + ramUsage, Logger.MessagePriority.Info);
            }
        }

        private void UserFSCleaner()
        {
            logger.WriteLine("Cleaning UserFS", Logger.MessagePriority.Debug);
            lock (userFSLock)
            {
                Dictionary<String, UserFS> newUserFSList = new System.Collections.Generic.Dictionary<string, UserFS>();
                foreach (KeyValuePair<String, UserFS> var in userFSList)
                {
                    if (IsUserLoggedIn(var.Key))
                    {
                        logger.WriteLine("User " + var.Key + " is logged in", Logger.MessagePriority.UserInfo);
                        newUserFSList.Add(var.Key, var.Value);
                    }
                    else
                    {
                        logger.WriteLine("User " + var.Key + " is not logged in", Logger.MessagePriority.UserInfo);
                    }
                }
                userFSList = newUserFSList;
            }
        }

        private void ProcessListCleaner()
        {
            logger.WriteLine("Cleaning process cache", Logger.MessagePriority.Debug);
            Dictionary<int, String> newUserProcList = new Dictionary<int, string>();
            System.Diagnostics.Process[] processlist = System.Diagnostics.Process.GetProcesses();

            lock (userProcList)
            {
                foreach (System.Diagnostics.Process proc in processlist)
                {
                    if (userProcList.ContainsKey(proc.Id))
                    {
                        String user = null;
                        userProcList.TryGetValue(proc.Id, out user);
                        if (user != null)
                        {
                            newUserProcList.Add(proc.Id, user);
                        }
                    }
                }
                userProcList = newUserProcList;
            }
        }

        private string GetPath(string filename)
        {
            string path = root_ + filename;
            logger.WriteLine("GetPath : " + path, Logger.MessagePriority.Debug);
            return path;
        }

        public int CreateFile(String filename, FileAccess access, FileShare share, FileMode mode, FileOptions options, DokanFileInfo info)
        {
            String userName = GetProcessOwner((int)info.ProcessId);
            string path = GetPath(filename);
            info.Context = count_++;
            logger.WriteLine("CreateFile: " + path + " //// " + mode.ToString(), Logger.MessagePriority.Debug);
            if (!(Directory.Exists(path)) && (mode == FileMode.Create || mode == FileMode.CreateNew || mode == FileMode.OpenOrCreate))
            {
                if (File.Exists(path))
                {
                    return 0;
                }
                RamFile fs = null;
                logger.WriteLine("Creating " + path, Logger.MessagePriority.Debug);
                if (!userFSList.ContainsKey(userName))
                {
                    //fs = new OpenedRamFile(root_ + filename);
                    UserFS ufs = new UserFS();
                    ufs.userName = userName;
                    ufs.files = new System.Collections.Generic.LinkedList<RamFile>();
                    //ufs.files.AddLast(fs);
                    lock (userFSLock)
                    {
                        userFSList.Add(userName, ufs);
                    }
                    AddUserFSRamFile(userName, filename);
                }
                else
                {
                    fs = GetUserFSFile(userName, filename);
                    if (fs == null)
                    {
                        lock (userFSLock)
                        {
                            fs = GetUserFSFile(userName, filename);
                            if (fs == null)
                            {
                                fs = AddUserFSRamFile(userName, filename);
                            }
                        }
                    }
                }
                return 0;
            }
            else if (File.Exists(path))
            {
                return 0;
            }
            else if (Directory.Exists(path))
            {
                info.IsDirectory = true;
                return 0;
            }
            else
            {
                UserFS ufs;
                if (userFSList.TryGetValue(userName, out ufs))
                {
                    foreach (RamFile f in ufs.files)
                    {
                        if (f.Name.ToLower().Equals(path.ToLower()))
                        {
                            return 0;
                        }
                    }
                }
                return -DokanNet.ERROR_FILE_NOT_FOUND;
            }
        }

        public int OpenDirectory(String filename, DokanFileInfo info)
        {
            info.Context = count_++;
            if (Directory.Exists(GetPath(filename)))
                return 0;
            else
                return -DokanNet.ERROR_PATH_NOT_FOUND;
        }

        public int CreateDirectory(String filename, DokanFileInfo info)
        {
            return -1;
        }

        public int Cleanup(String filename, DokanFileInfo info)
        {
            //logger.WriteLine("%%%%%% count = {0}", info.Context);
            return 0;
        }

        public int CloseFile(String filename, DokanFileInfo info)
        {
            return 0;
        }

        public int ReadFile(String filename, Byte[] buffer, ref uint readBytes, long offset, DokanFileInfo info)
        {
            RamFile fs = null;
            String userName = GetProcessOwner((int)info.ProcessId);
            try
            {
                if (!userFSList.ContainsKey(userName))
                {
                    fs = new OpenedFile(root_ + filename);
                    UserFS ufs = new UserFS();
                    ufs.userName = userName;
                    ufs.files = new System.Collections.Generic.LinkedList<RamFile>();
                    ufs.files.AddLast(fs);
                    lock (userFSLock)
                    {
                        userFSList.Add(userName, ufs);
                    }
                }
                else
                {
                    fs = GetUserFSFile(userName, filename);
                    if (fs == null)
                    {
                        lock (userFSLock)
                        {
                            fs = GetUserFSFile(userName, filename);
                            if (fs == null)
                            {
                                fs = AddUserFSFile(userName, filename);
                            }
                        }
                    }
                }
                fs.Seek(offset, SeekOrigin.Begin);
                readBytes = (uint)fs.Read(buffer, 0, buffer.Length);
                return 0;
            }
            catch (Exception e)
            {
                logger.WriteLine("ReadFile error: " + e.Message + "     " + e.StackTrace, Logger.MessagePriority.None);
                return -1;
            }

        }

        public int WriteFile(String filename, Byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info)
        {
            RamFile fs = null;
            String userName = GetProcessOwner((int)info.ProcessId);
            try
            {
                if (!userFSList.ContainsKey(userName))
                {
                    fs = new OpenedFile(root_ + filename);
                    UserFS ufs = new UserFS();
                    ufs.userName = userName;
                    ufs.files = new System.Collections.Generic.LinkedList<RamFile>();
                    ufs.files.AddLast(fs);
                    lock (userFSLock)
                    {
                        userFSList.Add(userName, ufs);
                    }
                }
                else
                {
                    fs = GetUserFSFile(userName, filename);
                    if (fs == null)
                    {
                        lock (userFSLock)
                        {
                            fs = GetUserFSFile(userName, filename);
                            if (fs == null)
                            {
                                fs = AddUserFSFile(userName, filename);
                            }
                        }
                    }
                }
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Write(buffer, 0, buffer.Length);
                writtenBytes = (uint)buffer.Length;
                return 0;
            }
            catch (Exception e)
            {
                logger.WriteLine("WriteFile error: " + e.Message + "     " + e.StackTrace, Logger.MessagePriority.None);
                return -1;
            }
        }

        public int FlushFileBuffers(String filename, DokanFileInfo info)
        {
            //return -1;
            logger.WriteLine("FlushFileBuffers", Logger.MessagePriority.Debug);
            return 0;
        }

        public int GetFileInformation(String filename, FileInformation fileinfo, DokanFileInfo info)
        {
            logger.WriteLine("GetFileInformation: " + fileinfo, Logger.MessagePriority.Debug);
            String userName = GetProcessOwner((int)info.ProcessId);
            string path = GetPath(filename);
            if (File.Exists(path))
            {
                FileInfo f = new FileInfo(path);

                fileinfo.Attributes = f.Attributes;
                fileinfo.CreationTime = f.CreationTime;
                fileinfo.LastAccessTime = f.LastAccessTime;
                fileinfo.LastWriteTime = f.LastWriteTime;

                RamFile fs = null;

                if (!userFSList.ContainsKey(userName))
                {
                    fileinfo.Length = f.Length;
                }
                else
                {
                    fs = GetUserFSFile(userName, filename);
                    if (fs != null)
                    {
                        fileinfo.Length = fs.Length;
                    }
                    else
                    {
                        fileinfo.Length = f.Length;
                    }
                }
                return 0;
            }
            else if (Directory.Exists(path))
            {
                DirectoryInfo f = new DirectoryInfo(path);

                fileinfo.Attributes = f.Attributes;
                fileinfo.CreationTime = f.CreationTime;
                fileinfo.LastAccessTime = f.LastAccessTime;
                fileinfo.LastWriteTime = f.LastWriteTime;
                fileinfo.Length = 0;// f.Length;
                return 0;
            }
            else if (GetUserFSFile(userName, filename) != null)
            {
                RamFile fs = GetUserFSFile(userName, filename);
                //fileinfo = fs.fileInfo;
                fileinfo.Attributes = fs.fileInfo.Attributes;
                fileinfo.CreationTime = fs.fileInfo.CreationTime;
                fileinfo.LastAccessTime = fs.fileInfo.LastAccessTime;
                fileinfo.LastWriteTime = fs.fileInfo.LastWriteTime;
                fileinfo.Length = fs.Length;
                return 0;
            }
            else
            {
                return -1;
            }
        }

        public int FindFiles(String filename, ArrayList files, DokanFileInfo info)
        {
            logger.WriteLine("FindFiles: " + filename, Logger.MessagePriority.Debug);
            string path = GetPath(filename);
            if (Directory.Exists(path))
            {
                String userName = GetProcessOwner((int)info.ProcessId);
                UserFS ufs;

                if (userFSList.TryGetValue(userName, out ufs))
                {
                    foreach (RamFile f in ufs.files)
                    {
                        String dir = Path.GetDirectoryName(f.Name) + "\\";
                        if (f is OpenedRamFile && dir.ToLower().Equals(path.ToLower()))
                        {
                            FileInformation fi = f.fileInfo;
                            //FileInformation fi = new FileInformation();
                            //fi.FileName = Path.GetFileName(f.Name);
                            //fi.Attributes = FileAttributes.Normal;
                            //fi.CreationTime = DateTime.Now;
                            //fi.LastAccessTime = DateTime.Now;
                            //fi.LastWriteTime = DateTime.Now;
                            fi.Length = f.Length;
                            files.Add(fi);
                        }
                    }
                }

                DirectoryInfo d = new DirectoryInfo(path);
                FileSystemInfo[] entries = d.GetFileSystemInfos();
                foreach (FileSystemInfo f in entries)
                {
                    FileInformation fi = new FileInformation();
                    fi.Attributes = f.Attributes;
                    fi.CreationTime = f.CreationTime;
                    fi.LastAccessTime = f.LastAccessTime;
                    fi.LastWriteTime = f.LastWriteTime;
                    if (f is DirectoryInfo)
                    {
                        fi.Length = 0;
                    }
                    else
                    {

                        if (!userFSList.ContainsKey(userName))
                        {
                            fi.Length = ((FileInfo)f).Length;
                        }
                        else
                        {
                            RamFile fs = null;
                            fs = GetUserFSFile(userName, f.FullName.Replace(root_, ""));
                            if (fs != null)
                            {
                                fi.Length = fs.Length;
                            }
                            else
                            {
                                fi.Length = ((FileInfo)f).Length;
                            }
                        }
                    }
                    //fi.Length = (f is DirectoryInfo) ? 0 : ((FileInfo)f).Length;
                    fi.FileName = f.Name;
                    files.Add(fi);
                }
                return 0;
            }
            else
            {
                return -1;
            }
        }

        public int SetFileAttributes(String filename, FileAttributes attr, DokanFileInfo info)
        {
            //return -1;
            logger.WriteLine("SetFileAttributes: Unsupported", Logger.MessagePriority.Debug);
            return 0;
        }

        public int SetFileTime(String filename, DateTime ctime,
                DateTime atime, DateTime mtime, DokanFileInfo info)
        {
            //return -1;
            logger.WriteLine("SetFileTime: Unsupported", Logger.MessagePriority.Debug);
            return 0;
        }

        public int DeleteFile(String filename, DokanFileInfo info)
        {
            logger.WriteLine("DeleteFile: " + filename, Logger.MessagePriority.Debug);
            RamFile fs = null;
            String userName = GetProcessOwner((int)info.ProcessId);
            fs = GetUserFSFile(userName, filename);
            if (fs != null)
            {
                fs.Close();
                RemoveUserFSFile(userName, fs);
            }
            return 0;
        }

        public int DeleteDirectory(String filename, DokanFileInfo info)
        {
            logger.WriteLine("DeleteDirectory: Unsupported", Logger.MessagePriority.Debug);
            return -1;
        }

        public int MoveFile(String filename, String newname, bool replace, DokanFileInfo info)
        {
            //return -1;
            logger.WriteLine("MoveFile: Unsupported", Logger.MessagePriority.Debug);
            return 0;
        }

        public int SetEndOfFile(String filename, long length, DokanFileInfo info)
        {
            logger.WriteLine("SetEndOfFile: " + filename + " to " + length, Logger.MessagePriority.Debug);
            RamFile fs = null;
            String userName = GetProcessOwner((int)info.ProcessId);

            fs = GetUserFSFile(userName, filename);
            if (fs != null)
            {
                fs.SetLength(length);
                return 0;
            }
            return -1;
        }

        public int SetAllocationSize(String filename, long length, DokanFileInfo info)
        {
            //return -1;
            logger.WriteLine("SetAllocationSize: Unsupported " + filename + " to " + length, Logger.MessagePriority.Debug);
            return 0;
        }

        public int LockFile(String filename, long offset, long length, DokanFileInfo info)
        {
            logger.WriteLine("LockFile: Unsupported", Logger.MessagePriority.Debug);
            return 0;
        }

        public int UnlockFile(String filename, long offset, long length, DokanFileInfo info)
        {
            logger.WriteLine("UnlockFile: Unsupported", Logger.MessagePriority.Debug);
            return 0;
        }

        public int GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes,
            ref ulong totalFreeBytes, DokanFileInfo info)
        {
            logger.WriteLine("GetDiskFreeSpace: Unsupported, fake values", Logger.MessagePriority.Debug);
            freeBytesAvailable = 512 * 1024 * 1024;
            totalBytes = 1024 * 1024 * 1024;
            totalFreeBytes = 512 * 1024 * 1024;
            return 0;
        }

        public int Unmount(DokanFileInfo info)
        {
            logger.WriteLine("Unmount", Logger.MessagePriority.Debug);
            return 0;
        }

        public string GetProcessOwner(int processId)
        {
            String user = null;
            if (!userProcList.TryGetValue(processId, out user))
            {

                string query = "Select * From Win32_Process Where ProcessID = " + processId;
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                ManagementObjectCollection processList = searcher.Get();

                foreach (ManagementObject obj in processList)
                {
                    string[] argList = new string[] { string.Empty, string.Empty };
                    int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
                    if (returnVal == 0)
                    {
                        // return DOMAIN\user
                        user = argList[1] + "\\" + argList[0];
                        lock (userProcList)
                        {
                            if (!userProcList.ContainsKey(processId))
                            {
                                userProcList.Add(processId, user);
                            }
                        }
                        return user;
                    }
                }
            }
            else
            {
                return user;
            }
            return "NO OWNER";
        }

        public bool IsUserLoggedIn(String userName)
        {
            ManagementScope scope = new ManagementScope("\\\\.\\root\\cimv2");

            //String query = "Select * from Win32_LogonSession Where LogonType = 2 OR LogonType = 10";
            String query = "Select * from Win32_LogonSession Where LogonType<>3 AND LogonType<>5 AND LogonType<>8 AND LogonType<>9 AND LogonType<>4";
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection userList = searcher.Get();

            foreach (ManagementObject obj in userList)
            {
                string logonID = obj["LogonId"].ToString();

                RelatedObjectQuery s2 = new RelatedObjectQuery("Associators of " + "{Win32_LogonSession.LogonId=" + logonID + "} " + "Where AssocClass=Win32_LoggedOnUser Role=Dependent");
                ManagementObjectSearcher userQuery = new ManagementObjectSearcher(scope, s2);
                ManagementObjectCollection users = userQuery.Get();
                foreach (ManagementObject user in users)
                {
                    String name = (String)user["Domain"] + "\\" + (String)user["Name"];
                    if (userName.Equals(name))
                    {
                        return true;
                    }
                }
            }

            return false;


        }

        private RamFile GetUserFSFile(String userName, String path)
        {
            RamFile fs = null;
            UserFS ufs;
            String fullPath = root_ + path;
            fullPath = fullPath.ToLower();
            bool exists = userFSList.TryGetValue(userName, out ufs);
            if (exists)
            {
                foreach (RamFile o in ufs.files)
                {
                    if (o.Name.ToLower().Equals(fullPath))
                    {
                        fs = o;
                    }
                }
            }
            return fs;
        }

        private void RemoveUserFSFile(String userName, String path)
        {
            RamFile fs = null;
            UserFS ufs;
            String fullPath = root_ + path;
            fullPath = fullPath.ToLower();
            bool exists = userFSList.TryGetValue(userName, out ufs);
            if (exists)
            {
                foreach (RamFile o in ufs.files)
                {
                    if (o.Name.ToLower().Equals(fullPath))
                    {
                        fs = o;
                    }
                }
                ufs.files.Remove(fs);
            }
        }

        private void RemoveUserFSFile(String userName, RamFile fs)
        {
            UserFS ufs;
            userFSList.TryGetValue(userName, out ufs);
            ufs.files.Remove(fs);
        }

        private RamFile AddUserFSFile(String userName, String path)
        {
            RamFile fs = null;
            fs = new OpenedFile(root_ + path);
            UserFS ufs;
            userFSList.TryGetValue(userName, out ufs);
            ufs.files.AddLast(fs);
            return fs;
        }

        private RamFile AddUserFSRamFile(String userName, String path)
        {
            RamFile fs = null;
            fs = new OpenedRamFile(root_ + path);
            fs.fileInfo = new FileInformation();
            fs.fileInfo.FileName = Path.GetFileName(fs.Name);
            fs.fileInfo.Attributes = FileAttributes.Normal;
            fs.fileInfo.CreationTime = DateTime.Now;
            fs.fileInfo.LastAccessTime = DateTime.Now;
            fs.fileInfo.LastWriteTime = DateTime.Now;
            UserFS ufs;
            userFSList.TryGetValue(userName, out ufs);
            ufs.files.AddLast(fs);
            return fs;
        }

        struct UserFS
        {
            public String userName;
            public System.Collections.Generic.LinkedList<RamFile> files;
        }
    }
}
