using System;
using System.Collections.Generic;
using System.Text;

namespace OverlayFS
{
    public class OpenedFile : RamFile
    {
        private String filePath;
        private Dictionary<Int64, Byte[]> overlay;
        private Int64 fileLength;
        private Int64 position = 0;
        private const int blockSize = 512;

        private System.IO.Stream baseStream;

        public OpenedFile(String path){
            baseStream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            filePath = path;
            overlay = new Dictionary<Int64, byte[]>();
            fileLength = baseStream.Length;
        }

        public override String Name
        {
            get
            {
                return filePath;
            }
        }

        public override void Write(byte[] array, int offset, int count)
        {
            //int block = offset / blockSize;
            foreach (byte b in array){
                //if (overlay.ContainsKey(offset))
                //{
                //    overlay.Remove(offset);
                //}
                //overlay.Add(offset, b);
                //if (offset >= fileLength)
                //{
                //    fileLength++;
                //}
                WriteByte(b);
                offset++;
            }
        }

        public override void WriteByte(byte value)
        {
            int block = (int)(this.Position / blockSize);
            int blockIndex = (int)(this.Position % blockSize);
            byte[] readBytes;

            if (overlay.ContainsKey(block))
            {
                //overlay.Remove(this.Position);
                overlay.TryGetValue(block, out readBytes);
            }
            else
            {
                baseStream.Seek(block * blockSize, System.IO.SeekOrigin.Begin);
                readBytes = new byte[blockSize];
                baseStream.Read(readBytes, 0, readBytes.Length);
                lock (overlay)
                {
                    overlay.Add(block, readBytes);
                }
            }
            readBytes[blockIndex] = value;
            //overlay.Add(this.Position, value);
            Int64 offset = this.Position;
            if (offset + 1 > fileLength)
            {
                fileLength++;
            }
            this.Position++;
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int readCount = 0;
            readCount = baseStream.Read(array, offset, count);

            long startingPosition = this.Position;
            long startingBlock = this.Position / blockSize;

            int tmpReadCount = 0;

            while (count > 0 && this.Position < this.Length)
            {
                long block = this.Position / blockSize;
                long blockIndex = this.Position % blockSize;
                byte[] b;
                bool found = overlay.TryGetValue(block, out b);
                if (found)
                {
                    int arrOffset = (int)((block - startingBlock) * blockSize);
                    Array.ConstrainedCopy(b, 0, array, arrOffset, blockSize);
                    //if last block was read
                    if (block == (this.Length / blockSize))
                    {
                        this.Position = this.Length;
                    }
                    else
                    {
                        this.Seek(blockSize, System.IO.SeekOrigin.Current);
                    }
                    if (this.Position >= baseStream.Length)
                    {
                        tmpReadCount = (int)(this.Position - startingPosition);
                    }
                }
                count -= blockSize;
                
            }
            
            readCount += tmpReadCount;
            this.Position = startingPosition + readCount;
            return readCount;
        }

        public override void SetLength(long value)
        {
            fileLength = value;

            Dictionary<Int64, Byte[]> newOverlay = new Dictionary<long, byte[]>();

            //Clean up overlay
            foreach (KeyValuePair<Int64, Byte[]> var in overlay)
            {
                if (var.Key <= value / blockSize)
                {
                    newOverlay.Add(var.Key, var.Value);
                }
            }
            overlay = newOverlay;
            GC.Collect();
        }

        public override long Length
        {
            get
            {
                return fileLength;
            }
        }

        public override void Close()
        {
            base.Close();
            fileLength = 0;
            overlay = null;
        }

        public override long Position
        {
            get
            {
                return position;
            }
            set
            {
                if (value >= baseStream.Length && value <= fileLength)
                {
                    position = value;
                }
                else if (value < baseStream.Length)
                {
                    position = value;
                    baseStream.Position = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        public override void Flush()
        {
            //throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            switch (origin)
            {
                case System.IO.SeekOrigin.Begin:
                    Position = offset;
                    break;
                case System.IO.SeekOrigin.Current:
                    Position = Position + offset;
                    break;
                case System.IO.SeekOrigin.End:
                    Position = Length + offset;
                    break;
                default:
                    break;
            }
            return Position;
        }

        public override long GetRamUsage()
        {
            return overlay.Values.Count * blockSize;
        }
    }
}
