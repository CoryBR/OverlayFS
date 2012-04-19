using System;
using System.Collections.Generic;
using System.Text;

namespace OverlayFS
{
    public abstract class RamFile : System.IO.Stream
    {
        public Dokan.FileInformation fileInfo;

        public abstract String Name
        {
            get;
        }

        public abstract long GetRamUsage();
    }
}
