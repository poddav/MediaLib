//! \file       MediaInfo.cs
//! \date       Thu Aug 04 08:13:53 2011
//! \brief      Provide information on various video formats.
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
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Rnd.MediaLib.Strings;
using Rnd.AVI;
using Rnd.Matroska;

namespace Rnd.MediaLib
{
    /// <summary>
    /// Exception raised when file format could not be recognized.
    /// </summary>
    public class InvalidFormat : System.Exception
    {
        public InvalidFormat () { }
        public InvalidFormat (string msg) : base (msg) { }
        public InvalidFormat (string msg, long position)
            : base (string.Format ("{0} @ {1:X8}", msg, position))
        { }
    }

    /// <summary>
    /// MediaFileInfo encapsulates information about media file.
    /// </summary>

    public class MediaFileInfo
    {
        private string m_path;
        public string FilePath  { get { return m_path; } }
        public string FileName  { get; set; }
        public string Type      { get; set; }
        public string Size      { get; set; }
        public string Duration  { get; set; }
        public string FrameSize { get; set; }
        public string Created   { get; set; }
        public string Info      { get; set; }
        public BitmapSource Icon { get; set; }

        /// <summary>
        /// Gather media-specific information from specified file.
        /// </summary>

        public MediaFileInfo (string filename)
        {
            m_path = filename;
            FileName = Path.GetFileName (filename);
            string ext = Path.GetExtension (filename).TrimStart('.').ToUpper();

            var shinfo = Rnd.Shell.FileInfo.GetInfo(filename,
                Rnd.Shell.FileInfo.SHGFI.TypeName | Rnd.Shell.FileInfo.SHGFI.Icon);

            if (shinfo != null)
            {
                try
                {
                    Type = shinfo.Value.szTypeName;
                    Icon = Imaging.CreateBitmapSourceFromHIcon (shinfo.Value.hIcon, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    Rnd.Shell.FileInfo.DestroyIcon (shinfo.Value.hIcon);
                }
            }
            else if (!string.IsNullOrEmpty(ext))
                Type = ext;
            else
                Type = wpfStrings.TextUnknown;

            Size = wpfStrings.TextUnknown;
            Duration = wpfStrings.TextUnknown;
            FrameSize = wpfStrings.TextUnknown;
            Created = wpfStrings.TextUnknown;
            Info = string.Empty;

            FileInfo fileinfo = new FileInfo (filename);
            if (fileinfo.Length > 1024*1024*1024)
            {
                double sz = (double)fileinfo.Length/1024/1024/1024;
                Size = string.Format (wpfStrings.TextSizeGBytes, sz);
            }
            else if (fileinfo.Length > 1024*1024)
            {
                double sz = (double)fileinfo.Length/1024/1024;
                Size = string.Format (wpfStrings.TextSizeMBytes, sz);
            }
            else
            {
                Size = string.Format (wpfStrings.TextSizeBytes, fileinfo.Length);
            }
            Created = fileinfo.CreationTime.ToString();

            try
            {
                if (ext == "AVI")
                    AviFileInfo();
                else if (ext == "MKV")
                    MkvFileInfo();
            }
            catch (EBML_Exception)
            {
                Info = wpfStrings.MsgInvalidFormat;
            }
            catch (InvalidFormat)
            {
                Info = wpfStrings.MsgInvalidFormat;
            }
            catch (System.Exception X)
            {
                Info = X.Message;
            }
        }

        private void AviFileInfo ()
        {
            Rnd.AVI.MediaInfo avi = new Rnd.AVI.MediaInfo (FilePath);

            FrameSize = string.Format ("{0}x{1}", avi.Width, avi.Height);

            double duration = avi.MicroSecPerFrame/1e6 * avi.TotalFrames;
            Duration = FormatDuration (duration);

            StringBuilder info = new StringBuilder();
            int stream_count = 0;
            foreach (var stream in avi.Streams)
            {
                if (stream.fccType == "auds")
                {
                    info.Append ("Audio: ");
                    switch (stream.Audio.wFormatTag)
                    {
                    default:
                        info.AppendFormat ("TAG:{0:X4}", stream.Audio.wFormatTag);
                        break;
                    case 0x0001:
                        info.Append ("PCM");
                        break;
                    case 0x0055:
                        info.Append ("MP3");
                        break;
                    case 0x0161:
                        info.Append ("WMA");
                        break;
                    case 0x0162:
                    case 0x0163:
                        info.Append ("WMA9");
                        break;
                    case 0x2000:
                        info.Append ("Dolby AC3");
                        break;
                    }
                    if (stream.Audio.nAvgBytesPerSec != 0)
                        info.AppendFormat (" {0}kbps", stream.Audio.nAvgBytesPerSec*8/1000);

                    if (stream.Audio.nChannels != 0)
                        info.Append (FormatAudioChannels (stream.Audio.nChannels));

                    if (stream.Audio.nSamplesPerSec != 0)
                        info.AppendFormat (" {0}Hz", stream.Audio.nSamplesPerSec);

                    info.AppendFormat (" [Stream {0}]\n", stream_count);
                }
                else if (stream.fccType == "vids")
                {
                    info.Append ("Video: ");
                    switch (stream.Video.biCompression)
                    {
                    default:
                        info.AppendFormat ("TAG:{0:X4}", stream.Video.biCompression);
                        break;
                    case 0x30355844:
                        info.Append ("DivX 5");
                        break;
                    case 0x44495658:
                        info.Append ("XviD");
                        break;
                    }
                    if (stream.Video.biWidth != 0 && stream.Video.biHeight != 0)
                        info.AppendFormat (" {0}x{1}", stream.Video.biWidth, stream.Video.biHeight);

                    if (avi.MicroSecPerFrame != 0)
                    {
                        double fps = 1e6/avi.MicroSecPerFrame;
                        info.Append (FormatFPS (fps));
                    }
                    if (avi.VideoBitRate != 0)
                        info.AppendFormat (" {0}kbps", (int)(avi.VideoBitRate/1000));

                    info.AppendFormat (" [Stream {0}]\n", stream_count);
                }
                ++stream_count;
            }
            Info = info.ToString();
        }

        private void MkvFileInfo ()
        {
            using (var mkv = new Rnd.Matroska.Reader (FilePath))
            {
                MkvTrack primary_video = null;
                uint video_count = 0, audio_count = 0, subs_count = 0;
                foreach (var track in mkv.Tracks)
                {
                    switch ((TrackType)track.Value.Type)
                    {
                    case TrackType.Video:
                        if (video_count == 0 || track.Value.Default)
                            primary_video = track.Value;
                        video_count++;
                        break;
                    case TrackType.Audio:	    audio_count++; break;
                    case TrackType.Subtitle:    subs_count++; break;
                    }
                }

                if (primary_video != null)
                    FrameSize = string.Format ("{0}x{1}",
                            primary_video.Width_px, primary_video.Height_px);

                Duration = FormatDuration (mkv.SegmentDuration);

                StringBuilder info = new StringBuilder();
                uint track_index = 1;
                uint video_num = 1, audio_num = 1, subs_num = 1;
                foreach (var track in mkv.Tracks)
                {
                    switch ((TrackType)track.Value.Type)
                    {
                    case TrackType.Video:
                        info.Append ("Video");
                        if (video_count > 1)
                            info.AppendFormat (" {0}", video_num++);

                        info.AppendFormat (": {0}", track.Value.CodecID);
                        if (track.Value.Width_px > 0)
                            info.AppendFormat (" {0}x{1}", track.Value.Width_px, track.Value.Height_px);
                        if (track.Value.FrameDuration > 0)
                            info.Append (FormatFPS (1e9 / track.Value.FrameDuration));
                        break;

                    case TrackType.Audio:
                        info.Append ("Audio");
                        if (audio_count > 1)
                            info.AppendFormat (" {0}", audio_num++);

                        info.AppendFormat (": {0}", track.Value.CodecID);
                        if (track.Value.SampleFreq > 0)
                            info.AppendFormat (" {0}Hz", (int)track.Value.SampleFreq);
                        
                        if (track.Value.Channels > 0)
                            info.Append (FormatAudioChannels ((int)track.Value.Channels));
                        break;

                    case TrackType.Subtitle:
                        info.Append ("Sub");
                        if (subs_count > 1)
                            info.AppendFormat (" {0}", subs_num++);

                        info.AppendFormat (": {0}", track.Value.CodecID);
                        break;

                    default:
                        info.Append ("Unknown: ");
                        break;
                    }
                    if (!string.IsNullOrEmpty (track.Value.Lang))
                        info.AppendFormat (" [{0}]", track.Value.Lang);
                    if (!string.IsNullOrEmpty (track.Value.Name))
                        info.AppendFormat (" [{0}]", track.Value.Name);
                    if (track.Value.Default)
                        info.Append (" (Default)");
                    info.Append ('\n');
                    ++track_index;
                }
                Info = info.ToString();
            }
        }

        public static string FormatDuration (double duration)
        {
            int hours = (int)(duration/60/60);
            if (hours != 0)
                return string.Format ("{0}:{1:00}:{2:00}", hours, (int)(duration/60)%60, duration%60);
            else
                return string.Format ("{0}:{1:00}", (int)(duration/60)%60, duration%60);
        }

        public static string FormatFPS (double fps)
        {
            if ((int)(fps*100)%100 == 0)
                return string.Format (" {0}FPS", (int)fps);
            else if ((int)(fps*100)%10 == 0)
                return string.Format (" {0:.0}FPS", fps);
            else
                return string.Format (" {0:.00}FPS", fps);
        }

        public static string FormatAudioChannels (int ch)
        {
            if (ch == 1)
                return " Mono";
            else if (ch == 2)
                return " Stereo";
            else
                return string.Format (" {0}Ch", ch);
        }
    }
}
