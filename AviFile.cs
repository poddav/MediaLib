//! \file       AviFile.cs
//! \date       Thu Aug 04 11:54:25 2011
//! \brief      get information from AVI stream.
//
// Copyright (C) 2011 by poddav
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using System.IO;
using System.Collections.Generic;

namespace Rnd.AVI
{
    public class MediaInfo
    {
        List<AviStream> m_streams = new List<AviStream>();

        public uint Length              { get; set; }
        public uint DataLength          { get; set; }
        public uint MicroSecPerFrame    { get; set; }
        public uint TotalFrames         { get; set; }
        public uint StreamCount         { get; set; }
        public uint Width               { get; set; }
        public uint Height              { get; set; }
        public double VideoBitRate      { get; set; }
        public IEnumerable<AviStream> Streams { get { return m_streams; } }

        public MediaInfo (string filename)
        {
            using (var input = new BinaryReader (File.OpenRead (filename), System.Text.Encoding.Default))
            {
                FourCC header = input.ReadUInt32();
                uint size = input.ReadUInt32();
                FourCC type = input.ReadUInt32();

                if (header != "RIFF" || type != "AVI " || size == 0)
                    throw new Rnd.MediaLib.InvalidFormat ("RIFF/AVI");

                Length = size;
                header.Assign (input.ReadUInt32());
                size = input.ReadUInt32();
                if (header != "LIST" || size == 0)
                    throw new Rnd.MediaLib.InvalidFormat ("LIST");

                type.Assign (input.ReadUInt32());
                header.Assign (input.ReadUInt32());
                size = input.ReadUInt32();
                if (type != "hdrl" || header != "avih" || size == 0)
                    throw new Rnd.MediaLib.InvalidFormat ("hdrl/avih");

                long pos  = input.BaseStream.Position;
                ReadAviHeader (input);

                for (;;)
                {
                    input.BaseStream.Seek (pos + size, SeekOrigin.Begin);
                    type.Assign (input.ReadUInt32());
                    size = input.ReadUInt32();
                    pos = input.BaseStream.Position;
                    if (size == 0)
                        break;
                    if (type == "JUNK")
                        continue;
                    /*
                    // Mutually exclusive with BitrateFromHeader() approach.
                    if (type == "idx1")
                    {
                        BitrateFromIndex (input, (int)size / 0x10);
                        break;
                    }
                    */
                    if (type != "LIST" || size == 0)
                        break;

                    header.Assign (input.ReadUInt32());
                    if (header == "strl")
                        m_streams.Add (ReadStreamHeader (input));
                    else if (header == "movi")
                    {
                        DataLength = size;
                        // abort parsing when data stream reached
                        break;
                    }
                }
            }
            BitrateFromHeader();
        }

        private void BitrateFromHeader ()
        {
            if (MicroSecPerFrame == 0)
                return;
            double audio_bps = 0;
            foreach (var stream in m_streams)
            {
                if (stream.fccType == "auds")
                    audio_bps += stream.Audio.nAvgBytesPerSec;
            }
            double data_length = DataLength != 0? DataLength: Length;
            data_length -= 8 * m_streams.Count * TotalFrames;
            double bps = data_length / TotalFrames / ((double)MicroSecPerFrame / 1e6);
            bps -= audio_bps;
            VideoBitRate = bps * 8;
        }

        private void BitrateFromIndex (BinaryReader input, int count)
        {
            if (MicroSecPerFrame == 0)
                return;
            long video_size = 0;
            for (int i = 0; i < count; ++i)
            {
                uint fcc = input.ReadUInt32();
                uint flags = input.ReadUInt32();
                uint offset = input.ReadUInt32();
                uint size = input.ReadUInt32();
                if (fcc == 0x63643030 || fcc == 0x62643030)
                    video_size += size;
            }
            VideoBitRate = (uint)(video_size / ((double)MicroSecPerFrame / 1e6) * 8 / TotalFrames);
        }

        private AviStream ReadStreamHeader (BinaryReader input)
        {
            FourCC fourcc = input.ReadUInt32();
            uint size = input.ReadUInt32();
            if (fourcc != "strh" || size == 0)
                throw new Rnd.MediaLib.InvalidFormat ("strh");
            long pos = input.BaseStream.Position;
            AviStream stream = new AviStream();
            stream.fccType    = input.ReadUInt32();
            stream.fccHandler = input.ReadUInt32();
            stream.dwFlags    = input.ReadUInt32();
            stream.wPriority  = input.ReadUInt16();
            stream.wLanguage  = input.ReadUInt16();
            stream.dwInitialFrames = input.ReadUInt32();
            stream.dwScale      = input.ReadUInt32();
            stream.dwRate       = input.ReadUInt32();
            stream.dwStart      = input.ReadUInt32();
            stream.dwLength     = input.ReadUInt32();
            stream.dwSuggestedBufferSize = input.ReadUInt32();
            stream.dwQuality    = input.ReadUInt32();
            stream.dwSampleSize = input.ReadUInt32();
            stream.left         = input.ReadInt16();
            stream.top          = input.ReadInt16();
            stream.right        = input.ReadInt16();
            stream.bottom       = input.ReadInt16();

            input.BaseStream.Seek (pos + size, SeekOrigin.Begin);
            fourcc.Assign (input.ReadUInt32());
            size = input.ReadUInt32();
            pos = input.BaseStream.Position;
            if (fourcc != "strf" || size == 0)
                throw new Rnd.MediaLib.InvalidFormat ("strf", pos);

            if (stream.fccType == "vids")
            {
                input.ReadUInt32();
                stream.Video.biWidth    = input.ReadInt32();
                stream.Video.biHeight   = input.ReadInt32();
                stream.Video.biPlanes   = input.ReadUInt16();
                stream.Video.biBitCount     = input.ReadUInt16();
                stream.Video.biCompression  = input.ReadUInt32();
                stream.Video.biSizeImage    = input.ReadUInt32();
                stream.Video.biXPelsPerMeter = input.ReadInt32();
                stream.Video.biYPelsPerMeter = input.ReadInt32();
                stream.Video.biClrUsed      = input.ReadUInt32();
                stream.Video.biClrImportant = input.ReadUInt32();
            }
            else if (stream.fccType == "auds")
            {
                stream.Audio.wFormatTag     = input.ReadUInt16();
                stream.Audio.nChannels      = input.ReadUInt16();
                stream.Audio.nSamplesPerSec = input.ReadUInt32();
                stream.Audio.nAvgBytesPerSec = input.ReadUInt32();
                stream.Audio.nBlockAlign    = input.ReadUInt16();
                stream.Audio.wBitsPerSample = input.ReadUInt16();
            }

            input.BaseStream.Seek (pos + size, SeekOrigin.Begin);
            for (;;)
            {
                fourcc.Assign (input.ReadUInt32());
                if (fourcc == "strn")
                {
                    size = input.ReadUInt32();
                    stream.Name = new string (input.ReadChars ((int)size));
                    continue;
                }
                if (fourcc == "strd")
                {
                    size = input.ReadUInt32();
                    input.BaseStream.Seek (size, SeekOrigin.Current);
                    continue;
                }
                input.BaseStream.Seek (-4, SeekOrigin.Current);
                break;
            }
            return stream;
        }

        private void ReadAviHeader(BinaryReader input)
        {
            MicroSecPerFrame        = input.ReadUInt32();
            uint dwMaxBytesPerSec   = input.ReadUInt32();
            uint dwPaddingGranularity= input.ReadUInt32();
            uint dwFlags            = input.ReadUInt32();
            TotalFrames             = input.ReadUInt32();
            uint dwInitialFrames    = input.ReadUInt32();
            StreamCount             = input.ReadUInt32();
            uint dwSuggestedBufferSize= input.ReadUInt32();
            Width                   = input.ReadUInt32();
            Height                  = input.ReadUInt32();
        }
    }

    public struct WAVEFORMAT
    {
        public ushort   wFormatTag;
        public ushort   nChannels;
        public uint     nSamplesPerSec;
        public uint     nAvgBytesPerSec;
        public ushort   nBlockAlign;
        public ushort   wBitsPerSample;
    }

    public struct BITMAPINFOHEADER
    {
        public int      biWidth;
        public int      biHeight;
        public ushort   biPlanes;
        public ushort   biBitCount;
        public uint     biCompression;
        public uint     biSizeImage;
        public int      biXPelsPerMeter;
        public int      biYPelsPerMeter;
        public uint     biClrUsed;
        public uint     biClrImportant;
    }

    public class AviStream
    {
        public string Name { get; set; }
        public FourCC fccType;
        public FourCC fccHandler;
        public uint   dwFlags;
        public ushort wPriority;
        public ushort wLanguage;
        public uint   dwInitialFrames;
        public uint   dwScale;
        public uint   dwRate;
        public uint   dwStart;
        public uint   dwLength;
        public uint   dwSuggestedBufferSize;
        public uint   dwQuality;
        public uint   dwSampleSize;

        public short left;
        public short top;
        public short right;
        public short bottom;

        public WAVEFORMAT Audio = new WAVEFORMAT();
        public BITMAPINFOHEADER Video = new BITMAPINFOHEADER();
    }

    public class FourCC
    {
        private char[] m_val = new char[4];

        public FourCC (byte[] val)
        {
            Assign (val);
        }

        public FourCC (uint val)
        {
            Assign (val);
        }

        public void Assign (byte[] val)
        {
            int last = System.Math.Min (4, val.Length);
            int i;
            for (i = 0; i < last; ++i)
                m_val[i] = (char)val[i];
            for ( ; i < 4; ++i)
                m_val[i] = ' ';
        }

        public void Assign (uint val)
        {
            m_val[0] = (char)(val & 0xff);
            m_val[1] = (char)((val >> 8) & 0xff);
            m_val[2] = (char)((val >> 16) & 0xff);
            m_val[3] = (char)((val >> 24) & 0xff);
        }

        public static implicit operator string (FourCC f)
        {
            return f.ToString();
        }
        public static implicit operator FourCC (byte[] val)
        {
            return new FourCC (val);
        }
        public static implicit operator FourCC (uint val)
        {
            return new FourCC (val);
        }
        public override string ToString()
        {
            return new string(m_val);
        }
    }
}
