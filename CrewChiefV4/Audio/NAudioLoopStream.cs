using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.Audio
{
    // Based on: http://mark-dot-net.blogspot.com/2009/10/looped-playback-in-net-with-naudio.html
    public class NAudioLoopStream : WaveStream
    {
        WaveStream sourceStream = null;

        public NAudioLoopStream(WaveStream sourceStream)
        {
            this.sourceStream = sourceStream;
            this.EnableLooping = true;
        }

        public bool EnableLooping { get; set; }

        public override WaveFormat WaveFormat
        {
            get { return this.sourceStream.WaveFormat; }
        }

        public override long Length
        {
            get { return this.sourceStream.Length; }
        }

        public override long Position
        {
            get { return this.sourceStream.Position; }
            set { this.sourceStream.Position = value; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                var bytesRead = this.sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    if (this.sourceStream.Position == 0 || !this.EnableLooping)
                    {
                        // something wrong with the source stream
                        break;
                    }
                    // loop
                    this.sourceStream.Position = 0;
                }
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }
    }
}
