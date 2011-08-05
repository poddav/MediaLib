//! \file       MatroskaTest.cs
//! \date       Fri Aug 05 11:53:54 2011
//! \brief      test Matroska interface
//

using System;
using System.IO;

namespace Rnd.Matroska
{
    class Test
    {
        static string Filename { get; set; }

        static void PrintInfo (Reader reader)
        {
            var finfo = new FileInfo (Filename);
            Console.WriteLine ("{0} {1:0,0} bytes", Filename, finfo.Length);

            string title = null;
            uint video_count = 0, audio_count = 0, subs_count = 0;
            foreach (var track in reader.Tracks)
            {
                switch ((TrackType)track.Value.Type)
                {
                case TrackType.Video:
                    if (title == null && !string.IsNullOrEmpty (track.Value.Name))
                        title = track.Value.Name;
                    video_count++;
                    break;
                case TrackType.Audio:	    audio_count++; break;
                case TrackType.Subtitle:    subs_count++; break;
                }
            }
            if (!string.IsNullOrEmpty (title))
                Console.WriteLine ("Title: {0}", title);

            double duration = reader.SegmentDuration;
            string duration_time = string.Format ("{0}:{1:00}:{2:00}",
                (int)(duration/60/60), (int)(duration/60)%60, (int)(duration % 60));

            uint track_index = 1;
            uint video_num = 1, audio_num = 1, subs_num = 1;
            foreach (var track in reader.Tracks)
            {
                switch ((TrackType)track.Value.Type)
                {
                case TrackType.Video:
                    Console.Write ("{0} video", track_index);
                    if (video_count > 1)
                        Console.Write (video_num++);

                    Console.Write (": {0} {1} ", track.Value.Lang, duration_time);
                    if (track.Value.Width_px > 0)
                        Console.Write ("{0}x{1} ", track.Value.Width_px, track.Value.Height_px);
                    if (track.Value.FrameDuration > 0)
                        Console.Write ("{0:.00}fps ", 1e9 / track.Value.FrameDuration);
                    Console.WriteLine (track.Value.CodecID);
                    break;

                case TrackType.Audio:
                    Console.Write ("{0} audio", track_index);
                    if (audio_count > 1)
                        Console.Write (audio_num++);
                    Console.Write (": {0} {1} ", track.Value.Lang, duration_time);
                    if (track.Value.SampleFreq > 0)
                        Console.Write ("{0}KHz ", (int)(track.Value.SampleFreq/1000));
                    if (track.Value.Channels > 0)
                        Console.Write ("{0}Ch ", track.Value.Channels);
                    Console.WriteLine (track.Value.CodecID);
                    break;

                case TrackType.Subtitle:
                    Console.Write ("{0} sub", track_index);
                    if (subs_count > 1)
                        Console.Write (subs_num++);
                    Console.Write (": {0} ", track.Value.Lang);
                    if (!string.IsNullOrEmpty (track.Value.Name))
                        Console.Write ("[{0}] ", track.Value.Name);
                    Console.WriteLine (track.Value.CodecID);
                    break;
                }
                ++track_index;
            }
            /*
            if (g_dump_segment)
            {
                const mkv_reader::mkv_segment& segment = reader.segment();
                Console.Write ("Segment info:\n";

                if (segment.date)
                {
                    time_t posixtime = segment.date/1e9 + 978296400;
                    Console.Write ("Date " << std::ctime (&posixtime);
                }
                if (!segment.filename.empty())
                    Console.Write ("Filename " << segment.filename << '\n';
                if (segment.uid.size() == 16)
                {
                    Console.Write ("UID ";
                    ext::format hexformat ("%02X");
                    std::for_each (segment.uid.begin(), segment.uid.end(),
                           [&] (uint8_t c) { Console.Write (hexformat% c; });
                    Console.Write ('\n';
                }
                if (!segment.mux_app.empty())
                    Console.Write ("Mux app: " << segment.mux_app << '\n';
            }
            */

            if (reader.Attachments.Count > 0)
            {
                Console.WriteLine ("Attached files:");
                foreach (var att in reader.Attachments)
                    Console.WriteLine ("{2,8}  {0,-20} {1}",
                        att.Name, att.MimeType, att.Size);
            }
        }

        public static void Main (string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine ("gimme MKV filename");
                return;
            }
            try
            {
                Filename = args[0];
                using (Reader mkv = new Reader (Filename, true))
                {
                    PrintInfo (mkv);
                }
            }
            catch (Exception X)
            {
                System.Console.Error.WriteLine ("[Exception] {0}", X.Message);
            }
        }
    }
}
