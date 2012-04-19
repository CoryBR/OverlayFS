using System;
using System.Collections.Generic;
using System.Text;

namespace OverlayFS
{
    class OpenedRamFile : RamFile
    {
        private String filePath;
        private System.IO.MemoryStream baseStream;

        public OpenedRamFile(String path)
        {
            filePath = path;
            baseStream = new System.IO.MemoryStream();
        }
        public override string Name
        {
            get { return filePath; }
        }

        public override bool CanRead
        {
            get { return baseStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return baseStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return baseStream.CanWrite; }
        }

        public override void Flush()
        {
            baseStream.Flush();
        }

        public override long Length
        {
            get { return baseStream.Length; }
        }

        public override long Position
        {
            get
            {
                return baseStream.Position;
            }
            set
            {
                baseStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            return baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            baseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            baseStream.Write(buffer, offset, count);
        }

        public override long GetRamUsage()
        {
            return baseStream.Length;
        }
    }
}
