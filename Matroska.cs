//! \file       Matroska.cs
//! \date       Fri Aug 05 07:17:16 2011
//! \brief      get information from MKV stream.
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

using System;
using System.IO;
using System.Collections.Generic;

namespace Rnd.Matroska
{
    /// <summary>
    /// Exception thrown on parse errors.
    /// </summary>
    public class EBML_Exception : System.Exception
    {
        public EBML_Exception (string msg) : base (msg) { }
    }

    /// <summary>
    /// EBML parser implementation.
    /// </summary>
    class EBMLParser
    {
        public delegate bool SizeChecker (ulong value);
        /// <summary>
        /// Delegate that parses EBML stream <paramref name="buf"/> in context of
        /// specified <paramref name="id"/>.
        /// If delegate doesn't know how to parse stream it should return false, leaving stream
        /// position intact.
        /// If parse somehow succeeded, delegate should return true, leaving stream position right
        /// after all parsed data.
        /// <summary>
        public delegate bool SectionParser (Stream buf, uint id);

        public EBMLParser ()
        {
            this.Version        = new Checked<uint>(1);
            ReadVersion         = new Checked<uint>(1);
            MaxIDLength         = new Checked<uint>(4);
            MaxSizeLength       = new Checked<uint>(8);
            DocType             = new Checked<string>("matroska");
            DocTypeVersion      = new Checked<uint>(1);
            DocTypeReadVersion  = new Checked<uint>(1);
        }

        /// <summary>Convenient size checkers</summary>
        public bool LongChecker (ulong x) { return x <= (ulong)long.MaxValue; }
        public bool IntChecker  (ulong x) { return x <= (ulong)int.MaxValue; }

        /// <returns>
        /// Number of leading 0-bits in <paramref name="mask"/>, starting at the most significant bit
        /// position, or -1 if there's no 1-bits in <paramref name="mask"/>.
        /// </returns>
        public static int BitScanMSB (uint mask)
        {
            if (mask == 0)
                return -1;
            int index = 0;
            while ((mask >>= 1) != 0)
                ++index;
            return 31-index;
        }
        public static int BitScanMSB (ushort mask)
        {
            int index = BitScanMSB ((uint)mask);
            return index == -1? -1: index - 16;
        }
        public static int BitScanMSB (byte mask)
        {
            int index = BitScanMSB ((uint)mask);
            return index == -1? -1: index - 24;
        }

        /// <summary>
        /// Read EBML ID sequence from stream <paramref="buf"/>, using
        /// <paramref="octet"/> as a first byte of sequence.
        /// </summary>
        public uint ReadID (Stream buf, int octet)
        {
            if (octet != -1)
            {
                int n = BitScanMSB ((byte)octet);
                if (n < 0 || n > MaxIDLength)
                    throw new EBML_Exception ("invalid EBML id size");

                uint id = (uint)(octet & 0xff);
                while (n-- != 0)
                    id = (id << 8) | (byte)(octet = buf.ReadByte());

                if (octet != -1)
                    return id;
            }
            throw new EBML_Exception ("premature end of file");
        }
        public uint ReadID (Stream buf)
        {
            return ReadID (buf, buf.ReadByte());
        }

        /// <summary>
        /// Read EBML size sequence from stream <paramref="buf"/>, checking
        /// obtained size using <paramref="check_size"/> delegate.
        /// </summary>
        public ulong ReadSize (Stream buf, SizeChecker check_size)
        {
            int octet = buf.ReadByte();
            if (octet != -1)
            {
                int n = BitScanMSB ((byte)octet);
                if (n < 0 || n > MaxSizeLength)
                    throw new EBML_Exception ("invalid EBML VINT size");

                ulong vint = (ulong)(octet & (0xff >> (n + 1)));
                while (n-- != 0)
                    vint = (vint << 8) | (byte)(octet = buf.ReadByte());

                if (octet != -1)
                {
                    if (check_size (vint))
                        return vint;
                    else
                        throw new EBML_Exception ("unsupported data size");
                }
            }
            throw new EBML_Exception ("premature end of file");
        }
        /// <summary>
        /// Read EBML size sequence from stream <paramref="buf"/>, using
        /// EBML.LongChecker delegate for constraints check.
        /// </summary>
        public ulong ReadSize (Stream buf)
        {
            return ReadSize (buf, LongChecker);
        }
        public void SkipData (Stream buf)
        {
            ulong data_size = ReadSize (buf);
            buf.Seek ((long)data_size, SeekOrigin.Current);
        }
        public uint ReadCRC32 (Stream buf)
        {
            SkipData (buf); // ignore crc
            return 0;
        }
        /// <summary>
        /// Read unsigned integer data from EBML stream <paramref="buf"/>.
        /// </summary>
        public ulong ReadUnsigned (Stream buf)
        {
            uint sz = (uint)ReadSize (buf, x => x <= 8);
            ulong num = 0;
            int octet = 0;
            while (sz-- != 0)
                num = (num << 8) | (byte)(octet = buf.ReadByte());
            if (octet == -1)
                throw new EBML_Exception ("premature end of file");
            return (num);
        }
        public uint ReadUInt (Stream buf)
        {
            ulong val = ReadUnsigned (buf);
            if (val > (ulong)uint.MaxValue)
                throw new EBML_Exception ("UINT32 value too big");
            return (uint)val;
        }
        /// <summary>
        /// Read signed integer data from EBML stream <paramref="buf"/>.
        /// </summary>
        public long ReadSigned (Stream buf)
        {
            uint sz = (uint)ReadSize (buf, x => x > 0 && x <= 8);
            int octet = buf.ReadByte();
            long num = (sbyte)octet;
            while (--sz != 0)
                num = (num << 8) | (byte)(octet = buf.ReadByte());
            if (octet == -1)
                throw new EBML_Exception ("premature end of file");
            return (num);
        }
        /// <summary>
        /// Read boolean value from EBML stream <paramref="buf"/>.
        /// </summary>
        public bool ReadBool (Stream buf)
        {
            return ReadUnsigned (buf) != 0;
        }
        /// <summary>
        /// Read DateTime value from EBML stream <paramref="buf"/>.
        /// </summary>
        /// <remarks>Note that value precision is cut by 100 since DateTime
        /// class measures time in 100ns intervals while EBML stores date
        /// values in nanoseconds offset from 2001-01-01.</remarks>
        public DateTime ReadDate (Stream buf)
        {
            long nanosec = ReadSigned (buf);
            // 2001-01-01T00:00:00,000000000 UTC
            DateTime date = new DateTime (2001, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return date.AddTicks (nanosec/100);
        }
        /// <summary>
        /// Read binary data from EBML stream <paramref="buf"/>.
        /// </summary>
        public byte[] ReadBinary (Stream buf)
        {
            ulong sz = ReadSize (buf, IntChecker);
            byte[] data = new byte[sz];
            buf.Read (data, 0, (int)sz);
            return data;
        }
        /// <summary>
        /// Read UTF-8 string from EBML stream <paramref="buf"/>.
        /// </summary>
        public string ReadString (Stream buf)
        {
            return System.Text.Encoding.UTF8.GetString (ReadBinary (buf));
        }

        /// <summary>
        /// Read floating point number from EBML stream <paramref="buf"/>.
        /// </summary>
        public double ReadFloat (Stream buf)
        {
            int sz = (int)ReadSize (buf, x => x == 4 || x == 8);

            byte[] data = new byte[sz];
            for (int i = sz-1; i >= 0; --i)
                data[i] = (byte)buf.ReadByte();

            if (sz == 4)
                return BitConverter.ToSingle (data, 0);
            else
                return BitConverter.ToDouble (data, 0);
        }

        /// <summary>
        /// Parse EBML stream <paramref="buf"/> using delegate <paramref="parser"/>.
        /// </summary>
        public void ParseSection (Stream buf, SectionParser parser)
        {
            ulong section_size = ReadSize (buf);
            long section_end = checked (buf.Position + (long)section_size);
            int octet;
            while ((octet = buf.ReadByte()) != -1 && buf.Position < section_end)
            {
                uint id = ReadID (buf, octet);
                switch (id)
                {
                default:
                    if (parser (buf, id)) break;
                    SkipData (buf);
                    break;
                case 0xec:  SkipData (buf); break;
                case 0xbf:  ReadCRC32 (buf); break;
                }
            }
            buf.Position = section_end;
        }

        public Checked<uint>    Version;
        public Checked<uint>    ReadVersion;
        public Checked<uint>    MaxIDLength;
        public Checked<uint>    MaxSizeLength;
        public Checked<string>  DocType;
        public Checked<uint>    DocTypeVersion;
        public Checked<uint>    DocTypeReadVersion;
    }

    /// <summary>
    /// Generic class that keeps track of controlled object modifications.
    /// </summary>
    class Checked<T>
    {
        /// <summary>
        /// Access to controlled value.
        /// </summary>
        public T Value
        {
            get { return m_value; }
            set
            {
                Modified = true;
                m_value = value;
            }
        }
        /// <summary>
        /// Returns true if value was modified since last reset, false otherwise.
        /// </summary>
        public bool Modified { get; private set; }

        public Checked ()
        {
            Modified = false;
        }

        public Checked (T val)
        {
            Reset (val);
        }

        /// <summary>
        /// Reset both controlled value and modification flag.
        /// </summary>
        public void Reset(T val)
        {
            m_value = val;
            Modified = false;
        }

        public static implicit operator T (Checked<T> v)
        {
            return v.m_value;
        }

        public override string ToString ()
        {
            return m_value.ToString();
        }

        private T       m_value;
    }

    /// <summary>
    /// Track type enumeration (matroska-specific)
    /// </summary>
    enum TrackType
    {
        Video       = 1,
        Audio       = 2,
        Complex     = 3,
        Logo        = 0x10,
        Subtitle    = 0x11,
        Buttons     = 0x12,
        Control     = 0x20,
    };

    /// <summary>
    /// Matroska data structures.
    /// </summary>
    class MkvTrack
    {
        public uint     Uid             { get; set; }
        public uint     Type            { get; set; }
        public bool     Enabled         { get; set; }
        public bool     Default         { get; set; }
        public bool     Forced          { get; set; }
        public bool     Lacing          { get; set; }
        public uint     FrameDuration   { get; set; }
        public double   Timescale       { get; set; }
        public string   Name            { get; set; }
        public string   Lang            { get; set; }
        public string   CodecID         { get; set; }
        public string   CodecName       { get; set; }
        public byte[]   CodecPrivate    { get; set; }

        // Video settings
        //
        public bool     Interlaced      { get; set; }
        public uint     StereoMode      { get; set; }
        public uint     Width_px        { get; set; }
        public uint     Height_px       { get; set; }
        public uint     CropBottom      { get; set; }
        public uint     CropTop         { get; set; }
        public uint     CropLeft        { get; set; }
        public uint     CropRight       { get; set; }
        public uint     DisplayWidth    { get; set; }
        public uint     DisplayHeight   { get; set; }
        public uint     DisplayUnit     { get; set; }
        public uint     AspectType      { get; set; }

        // Audio settings
        //
        public double   SampleFreq      { get; set; }
        public double   OutSampleFreq   { get; set; }
        public uint     Channels        { get; set; }
        public uint     BitDepth        { get; set; }
    }

    class MkvSegment
    {
        public long         Offset      { get; set; }
        public ulong        Size        { get; set; }
        public double       Duration    { get; set; }
        public ulong        Timescale   { get; set; }
        public DateTime     Date        { get; set; }
        public string       Uid         { get; set; }
        public string       Filename    { get; set; }
        public string       Title       { get; set; }
        public string       MuxApp      { get; set; }
        public string       WriteApp    { get; set; }

        public MkvSegment ()
        {
            Offset = 0;
            Size = 0;
            Duration = 0;
            Timescale = 1000000;
        }
    };

    class MkvBlock
    {
        public uint         TrackNumber { get; set; }
        public long         Timecode    { get; set; }
        public ulong        Duration    { get; set; }
        public byte[]       Data        { get; set; }
        public uint         Size        { get; set; }
//        public MkvExtractor       extractor;
    };

    class MkvAttachment
    {
        public string       Name        { get; set; }
        public string       Description { get; set; }
        public string       MimeType    { get; set; }
        public uint         Uid         { get; set; }
        public string       Referral    { get; set; }
        public ulong        StartTime   { get; set; }
        public ulong        EndTime     { get; set; }
        public long         Offset      { get; set; }
        public ulong        Size        { get; set; }
    };

    enum EBML_ID : uint
    {
        Chapters    = 0x1043a770,
        MetaSeek    = 0x114d9b74,
        Tags        = 0x1254c367,
        SegmentInfo = 0x1549a966,
        Track       = 0x1654ae6b,
        Segment     = 0x18538067,
        Attachment  = 0x1941a469,
        Header      = 0x1a45dfa3,
        Signature   = 0x1b538667,
        CueingData  = 0x1c53bb6b,
        Cluster     = 0x1f43b675,
        SkipData    = 0xec,
        CRC32       = 0xbf,
    };

    /// <summary>
    /// Matroska parser class.
    /// </summary>
    class Reader : IDisposable
    {
        public const uint HeaderThreshold = 0x4000;
        
        private FileStream                  m_map;
        private Dictionary<uint, long>      m_seek = new Dictionary<uint,long>();
        private List<MkvSegment>            m_segments = new List<MkvSegment>();
        private List<MkvAttachment>         m_attachment = new List<MkvAttachment>();
        private Dictionary<uint, MkvTrack>  m_tracks = new Dictionary<uint,MkvTrack>();

        /// <summary>
        /// File offset of the first data cluster.
        /// </summary>
        private long                        m_first_cluster = 0;
        /// <summary>
        /// True if segment info has been read.
        /// </summary>
        private bool                        m_read_segment_info = false;
        /// <summary>
        /// True if file has attachments section.
        /// </summary>
        private bool                        m_have_attachments = false;

        public delegate bool SectionParser (uint id);

        public bool                         _Trace  { get; set; }
        public EBMLParser                   EBML    { get; private set; }
        public MkvSegment                   Segment
        {
            get { return m_segments[0]; }
        }
        public Dictionary<uint, MkvTrack>   Tracks
        {
            get { return m_tracks; }
        }
        public List<MkvAttachment>          Attachments
        {
            get
            {
                if (m_have_attachments && m_attachment.Count == 0)
                    LoadAttachments();
                return m_attachment;
            }
        }
        public double                       SegmentDuration
        {
            get { return (double)Segment.Duration/1e9 * Segment.Timescale; }
        }

        public Reader (string filename, bool trace = false)
        {
            _Trace = trace;

            m_map = new FileStream (filename, FileMode.Open, FileAccess.Read);
            try
            {
                Parse();
            }
            catch
            {
                m_map.Close();
                throw;
            }
        }

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        protected virtual void Dispose (bool disposing)
        {
            if (!this.m_disposed)
            {
                if (disposing)
                {
                    // free managed resources
                    m_map.Close();
                }
                m_disposed = true;
            }
        }

        ~Reader ()
        {
            Dispose (false);
        }

        private void Parse ()
        {
            if (!FindHeader (m_map))
                throw new EBML_Exception ("MKV signature not found");

            InitDefaults();
            ParseHeader();
            ParseCore();

            // Read extra data if seek metadata is available
            //
            if (m_seek.Count != 0)
            {
                if (SeekTo (EBML_ID.MetaSeek))
                    ParseMetaSeek();

                if (!m_read_segment_info && SeekTo (EBML_ID.SegmentInfo))
                    ParseSegmentInfo();

                if (m_tracks.Count == 0 && SeekTo (EBML_ID.Track))
                    ParseTrack();

                if (!m_have_attachments && m_seek.ContainsKey ((uint)EBML_ID.Attachment))
                    m_have_attachments = true;
            }
        }

        /// <summary>
        /// Look for Matroska signature in the first HeaderThreshold bytes
        /// of the specfied stream.
        /// </summary>
        /// <returns>True if Matroska signature was found, false otherwise.</returns>
        /// <remarks>Search is also aborted whenever soft End-of-file (0x1a) encountered.</remarks>
        public static bool FindHeader (Stream buf)
        {
            int ch;
            for (uint count = 0; count < HeaderThreshold; ++count)
            {
                ch = buf.ReadByte();
                if (ch == 0x1a)
                    return (buf.ReadByte() == 0x45 &&
                            buf.ReadByte() == 0xdf &&
                            buf.ReadByte() == 0xa3);
                if (ch == -1)
                    break;
            }
            return false;
        }

        private void InitDefaults ()
        {
            EBML = new EBMLParser();

            m_segments.Clear();
            m_segments.Add (new MkvSegment());
        }

        /// <summary>
        /// Make sure there's at least <paramref name="size"/> bytes available for reading.
        /// </summary>
        private void StreamReserve (int size)
        {
            if (size > m_map.Length || m_map.Position > m_map.Length - size)
                throw new EBML_Exception ("Premature end of file");
        }
        private void StreamReserve (ulong size)
        {
            if (size > (ulong)long.MaxValue || (long)size > m_map.Length || m_map.Position > m_map.Length - (long)size)
                throw new EBML_Exception ("Premature end of file");
        }

        private void ParseHeader ()
        {
            int header_size = (int)EBML.ReadSize (m_map, EBML.IntChecker);
            StreamReserve (header_size);

            for (long header_end = m_map.Position + header_size; m_map.Position < header_end; )
            {
                uint id = EBML.ReadID (m_map);
                switch (id)
                {
                case 0xec:      EBML.SkipData (m_map); break;
                case 0xbf:      EBML.ReadCRC32 (m_map); break;
                case 0x4286:    EBML.Version.Value = EBML.ReadUInt (m_map); break;
                case 0x42f7:    EBML.ReadVersion.Value = EBML.ReadUInt (m_map); break;
                case 0x42f2:    EBML.MaxIDLength.Value = EBML.ReadUInt (m_map); break;
                case 0x42f3:    EBML.MaxSizeLength.Value = EBML.ReadUInt (m_map); break;
                case 0x4287:    EBML.DocTypeVersion.Value = EBML.ReadUInt (m_map); break;
                case 0x4285:    EBML.DocTypeReadVersion.Value = EBML.ReadUInt (m_map); break;
                case 0x4282:    EBML.DocType.Value = EBML.ReadString (m_map); break;
                default:        throw new EBML_Exception ("Invalid header data");
                }
            }
            if (!(EBML.DocTypeVersion.Modified && EBML.DocTypeReadVersion.Modified
                  && EBML.DocType.Modified))
                throw new EBML_Exception ("Incomplete header");
        }

        private void ParseCore ()
        {
            if (EBML.DocType != "matroska")
                throw new EBML_Exception ("unsupported DocType");
            if (EBML.ReadVersion > 1)
                throw new EBML_Exception ("incompatible EBML version");
            if (EBML.DocTypeReadVersion > 3)
                throw new EBML_Exception ("incompatible DocType version");
            if (EBML.MaxIDLength > 4)
                throw new EBML_Exception ("unsupported max id length");
            if (EBML.MaxSizeLength > 8)
                throw new EBML_Exception ("unsupported max data size");

            int octet;
            while (m_first_cluster == 0 && (octet = m_map.ReadByte()) != -1)
            {
                long off = m_map.Position-1;
                uint id = EBML.ReadID (m_map, octet);
                switch ((EBML_ID)id)
                {
                default:
                    if (_Trace)
                        Console.Error.WriteLine ("> unknown section id {0:x04} @ {1:x8}, skipped", id, off);
                    EBML.SkipData (m_map);
                    break;
                case EBML_ID.SkipData:      EBML.SkipData (m_map); break;
                case EBML_ID.CRC32:         EBML.ReadCRC32 (m_map); break;
                case EBML_ID.Header:        ParseHeader(); break;
                case EBML_ID.Segment:
                    {
                        if (m_segments[0].Offset != 0)
                        {
                            if (_Trace)
                                Console.Error.WriteLine ("> MKV multiple segments not supported");
                            EBML.SkipData (m_map);
                            return;
                        }
                        m_segments[0].Size = EBML.ReadSize (m_map);
                        m_segments[0].Offset = m_map.Position;
                        if (_Trace)
                            Console.WriteLine (". new segment @ {0:x8} [{1} bytes]",
                                m_segments[0].Offset, m_segments[0].Size);
                        break;
                    }
                case EBML_ID.MetaSeek:      ParseMetaSeek(); break;
                case EBML_ID.SegmentInfo:   ParseSegmentInfo(); break;
                case EBML_ID.Track:         ParseTrack(); break;
                case EBML_ID.CueingData:    ParseCueData(); break;
                case EBML_ID.Chapters:      ParseChapters(); break;
                case EBML_ID.Cluster:
                    if (_Trace)
                        Console.WriteLine (".. first cluster data @ {0:x8}", off);
                    m_first_cluster = off;
                    break;

                case EBML_ID.Attachment:
                    m_seek[id] = off - m_segments[0].Offset;
                    m_have_attachments = true;
                    EBML.SkipData (m_map);
                    break;
                }
            }
        }

        public bool SeekTo (EBML_ID id, int segment = 0)
        {
            long offset;
            if (m_seek.TryGetValue ((uint)id, out offset))
            {
                m_map.Position = m_segments[segment].Offset + offset;
                return EBML.ReadID (m_map) == (uint)id;
            }
            return false;
        }

        void ParseSection (SectionParser parser)
        {
            ulong section_size = EBML.ReadSize (m_map);
            long section_end = checked (m_map.Position + (long)section_size);
            while (m_map.Position < section_end)
            {
                long off = m_map.Position;
                uint id = EBML.ReadID (m_map);
                switch (id)
                {
                default:
                    if (parser (id)) break;
                    ErrorHandler (id, off);
                    EBML.SkipData (m_map);
                    break;
                case 0xec:  EBML.SkipData (m_map); break;
                case 0xbf:  EBML.ReadCRC32 (m_map); break;
                }
            }
            m_map.Position = section_end;
        }

        void ErrorHandler (uint id, long offset)
        {
            if (_Trace)
                Console.Error.WriteLine ("> unknown data id {0:x04} @ {0:x8}, skipped", id, offset);
        }

        MemoryStream LocalStream (int size)
        {
            byte[] data = new byte[size];
            if (m_map.Read (data, 0, size) != size)
                throw new EBML_Exception ("Premature end of file");
            return new MemoryStream (data, 0, size, false);
        }

        void ParseMetaseekData (Stream input)
        {
            long map_offset = m_map.Position;

            var seek_id = new Checked<uint> (0);
            var seek_pos = new Checked<long> (0);

            int octet;
            while ((octet = input.ReadByte()) != -1)
            {
                long off = map_offset + input.Position;
                uint id = EBML.ReadID (input, octet);
                switch (id)
                {
                default:
                    if (_Trace)
                        Console.Error.WriteLine ("> unknown id {0:x00} @ {1:x8}", id, off);
                    EBML.SkipData (input);
                    break;
                case 0xec:  EBML.SkipData (input); break;
                case 0xbf:  EBML.ReadCRC32 (input); break;
                case 0x53ab:
                    {
                        if (seek_id.Modified)
                        {
                            if (_Trace)
                                Console.Error.WriteLine ("> duplicate seek data @ {0:x8}", off);
                            EBML.SkipData (input);
                            break;
                        }
                        uint id_size = (uint)EBML.ReadSize (input);
                        if (id_size > EBML.MaxIDLength)
                        {
                            if (_Trace)
                                Console.Error.WriteLine ("> seek id size {0} @ {1:x8}", id_size, off);
                            input.Seek ((long)id_size, SeekOrigin.Current);
                            break;
                        }
                        uint sid = 0;
                        while (id_size-- != 0)
                            sid = (sid << 8) | (byte)input.ReadByte();
                        seek_id.Value = sid;
                        break;
                    }
                case 0x53ac:
                    {
                        if (seek_pos.Modified)
                        {
                            if (_Trace)
                                Console.Error.WriteLine ("> duplicate seek data @ {0:x8}", off);
                            EBML.SkipData (input);
                            break;
                        }
                        seek_pos.Value = (long)EBML.ReadUnsigned (input);
                        break;
                    }
                }
            }
            if (seek_id.Modified && seek_pos.Modified)
                m_seek[seek_id] = seek_pos;
        }

        void ParseMetaSeek()
        {
            if (_Trace)
                Console.WriteLine (". ParseMetaSeek");
            ParseSection ((id) =>
            {
                if (id == 0x4dbb)
                {
                    int size = (int)EBML.ReadSize (m_map, EBML.IntChecker);
                    using (var input = LocalStream (size))
                        ParseMetaseekData (input);
                    return true;
                }
                return false;
            });
        }

        void ParseSegmentInfo()
        {
            if (_Trace)
                Console.WriteLine (". ParseSegmentInfo");

            int info_size = (int)EBML.ReadSize (m_map, EBML.IntChecker);
            if (_Trace)
                Console.WriteLine (".. segment info [{0} bytes]", info_size);

            StreamReserve (info_size);
            using (var input = LocalStream (info_size))
            {
                MkvSegment segment = m_segments[0];

                int octet;
                while ((octet = input.ReadByte()) != -1)
                {
                    uint id = EBML.ReadID (input, octet);
                    switch (id)
                    {
                    case 0x2ad7b1:  
                        segment.Timescale = EBML.ReadUnsigned (input);
                        m_read_segment_info = true;
                        break;
                    case 0x4461:    segment.Date = EBML.ReadDate (input); break;
                    case 0x4489:    segment.Duration = EBML.ReadFloat (input); break;
                    case 0x4d80:    segment.MuxApp = EBML.ReadString (input); break;
                    case 0x5741:    segment.WriteApp = EBML.ReadString (input); break;
                    case 0x7ba9:    segment.Title = EBML.ReadString (input); break;
                    case 0x73a4:    segment.Uid = EBML.ReadString (input); break;
                    case 0x7384:    segment.Filename = EBML.ReadString (input); break;
                    case 0x3cb923:
                    case 0x3c83ab:
                    case 0x3eb923:
                    case 0x3e83bb:
                    case 0x4444:
                    case 0x6924:
                    case 0x69fc:
                    case 0x69bf:
                    case 0x69a5:
                        if (_Trace)
                            Console.Error.WriteLine ("... id {0:x04}", id);
                        EBML.SkipData (input);
                        break;
                    default:
                    case 0xec:  EBML.SkipData (input); break;
                    case 0xbf:  EBML.ReadCRC32 (input); break;
                    }
                }
            }
        }

        private void ParseTrack()
        {
            if (_Trace)
                Console.WriteLine (". ParseTrack");
            ParseSection ((id) =>
            {
                if (id == 0xae)
                {
                    int size = (int)EBML.ReadSize (m_map, EBML.IntChecker);
                    using (var input = LocalStream (size))
                        ParseTrackData (input);
                    return true;
                }
                return false;
            });
        }

        private void ParseTrackData (Stream input)
        {
            uint track_number = 0;
            MkvTrack track_info = new MkvTrack();
            track_info.FrameDuration = 0;
            track_info.Timescale = 1;

            int octet;
            while ((octet = input.ReadByte()) != -1)
            {
                switch (EBML.ReadID (input, octet))
                {
                default:
                case 0xec:      EBML.SkipData (input); break;
                case 0xbf:      EBML.ReadCRC32 (input); break;
                case 0xd7:      track_number = EBML.ReadUInt (input); break;
                case 0x73c5:    track_info.Uid = EBML.ReadUInt (input); break;
                case 0x83:      track_info.Type = EBML.ReadUInt (input); break;
                case 0xb9:      track_info.Enabled = EBML.ReadBool (input); break;
                case 0x88:      track_info.Default = EBML.ReadBool (input); break;
                case 0x55aa:    track_info.Forced = EBML.ReadBool (input); break;
                case 0x9c:      track_info.Lacing = EBML.ReadBool (input); break;
                case 0x23e383:  track_info.FrameDuration = EBML.ReadUInt (input); break;
                case 0x23314f:  track_info.Timescale = EBML.ReadFloat (input); break;
                case 0x536e:    track_info.Name = EBML.ReadString (input); break;
                case 0x22b59c:  track_info.Lang = EBML.ReadString (input); break;
                case 0x86:      track_info.CodecID = EBML.ReadString (input); break;
                case 0x258688:  track_info.CodecName = EBML.ReadString (input); break;
                case 0x63a2:    track_info.CodecPrivate = EBML.ReadBinary (input); break;
                case 0xe0:
                    EBML.ParseSection (input, (buf, id) =>
                    {
                        switch (id)
                        {
                        default:        return false;
                        case 0x9a:      track_info.Interlaced   = EBML.ReadBool (buf); break;
                        case 0x53b8:    track_info.StereoMode   = EBML.ReadUInt (buf); break;
                        case 0xb0:      track_info.Width_px     = EBML.ReadUInt (buf); break;
                        case 0xba:      track_info.Height_px    = EBML.ReadUInt (buf); break;
                        case 0x54aa:    track_info.CropBottom   = EBML.ReadUInt (buf); break;
                        case 0x54bb:    track_info.CropTop      = EBML.ReadUInt (buf); break;
                        case 0x54cc:    track_info.CropLeft     = EBML.ReadUInt (buf); break;
                        case 0x54dd:    track_info.CropRight    = EBML.ReadUInt (buf); break;
                        case 0x54b0:    track_info.DisplayWidth = EBML.ReadUInt (buf); break;
                        case 0x54ba:    track_info.DisplayHeight= EBML.ReadUInt (buf); break;
                        case 0x54b2:    track_info.DisplayUnit  = EBML.ReadUInt (buf); break;
                        case 0x54b3:    track_info.AspectType   = EBML.ReadUInt (buf); break;
                        }
                        return true;
                    });
                    break;
                case 0xe1:
                    EBML.ParseSection (input, (buf, id) =>
                    {
                        switch (id)
                        {
                        default:        return false;
                        case 0xb5:      track_info.SampleFreq   = EBML.ReadFloat (buf); break;
                        case 0x78b5:    track_info.OutSampleFreq= EBML.ReadFloat (buf); break;
                        case 0x9f:      track_info.Channels     = EBML.ReadUInt (buf); break;
                        case 0x6264:    track_info.BitDepth     = EBML.ReadUInt (buf); break;
                        }
                        return true;
                    });
                    break;
                }
            }
            if (track_number != 0)
                m_tracks[track_number] = track_info;
        }

        void ParseCueData()
        {
            if (_Trace)
                Console.WriteLine (". ParseCueData");
            EBML.SkipData (m_map);
        }

        void LoadAttachments()
        {
            if (SeekTo (EBML_ID.Attachment))
                ParseAttachment();
            else if (_Trace)
                Console.Error.Write ("> Seek to attachments section failed");
        }

        void ParseAttachment()
        {
            if (_Trace)
                Console.WriteLine (". ParseAttachment");

            ulong att_size = EBML.ReadSize (m_map);
            if (_Trace)
                Console.WriteLine (".. attachments [{0} bytes]", att_size);

            StreamReserve (att_size);
            long att_end = m_map.Position + (long)att_size;
            while (m_map.Position < att_end)
            {
                uint id = EBML.ReadID (m_map);
                long off = m_map.Position;
                if (id != 0x61a7)
                {
                    if (_Trace)
                        Console.Error.WriteLine ("> invalid attachment data id {0:x04} @ {1:x8}", id, off);
                    break;
                }
                ulong size = EBML.ReadSize (m_map, x => x <= (ulong)(att_end - m_map.Position));
                ParseAttachmentData (size);
            }
            m_map.Position = att_end;
        }

        void ParseAttachmentData (ulong size)
        {
            MkvAttachment file = new MkvAttachment();
            bool got_name = false, got_mime = false, got_data = false, got_uid = false;

            long map_offset = m_map.Position;
            using (var buf = LocalStream ((int)size))
            {
                int octet;
                while ((octet = buf.ReadByte()) != -1)
                {
                    uint id = EBML.ReadID (buf, octet);
                    switch (id)
                    {
                    default:
                    case 0xec:      EBML.SkipData (buf); break;
                    case 0xbf:      EBML.ReadCRC32 (buf); break;
                    case 0x466e:    file.Name       = EBML.ReadString (buf); got_name = true; break;
                    case 0x467e:    file.Description= EBML.ReadString (buf); break;
                    case 0x4660:    file.MimeType   = EBML.ReadString (buf); got_mime = true; break;
                    case 0x465c:
                                    file.Size       = EBML.ReadSize (buf);
                                    file.Offset     = map_offset + buf.Position;
                                    buf.Seek ((long)file.Size, SeekOrigin.Current);
                                    got_data = true;
                                    break;
                    case 0x46ae:    file.Uid        = EBML.ReadUInt (buf); got_uid = true; break;
                    case 0x4675:    file.Referral   = EBML.ReadString (buf); break;
                    case 0x4661:    file.StartTime  = EBML.ReadUnsigned (buf); break;
                    case 0x4662:    file.EndTime    = EBML.ReadUnsigned (buf); break;
                    }
                }
            }
            if (got_name && got_mime && got_data && got_uid)
                m_attachment.Add (file);
        }

        public void ParseChapters()
        {
            if (_Trace)
                Console.WriteLine (". ParseChapters");
            EBML.SkipData (m_map);
            /*
            EBML.parse_section ([=] (id_type id) -> bool {
                if (id == 0x45b9)
                {
                    size_type size = EBML.read_size (m_map);
                    EBML.reserve (size);
                    mkv_parse_chapter_data (m_map.gdata(), size);
                    m_map.pubseekoff (size, std::ios::cur);
                    return true;
                }
                return false;
            }, default_error_handler());
            */
        }

        private bool m_disposed = false;
    }
}
