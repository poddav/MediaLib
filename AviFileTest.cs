//! \file       AviFileTest.cs
//! \date       Thu Aug 04 14:14:12 2011
//! \brief      
//

using System;

namespace Rnd.AVI
{
    public class Test
    {
        static int Main (string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine ("USAGE: aviinfo <filename>");
                return 1;
            }
            try
            {
                MediaInfo info = new MediaInfo (args[0]);
                double duration = info.MicroSecPerFrame/1e6 * info.TotalFrames;
                Console.WriteLine ("{0}x{1}, {2:.00} FPS, {3}:{4:00}:{5:00}",
                        info.Width, info.Height, 1.0/(info.MicroSecPerFrame/1e6),
                        (int)(duration/60/60), (int)(duration/60)%60, duration%60);
                int stream_count = 0;
                foreach (var stream in info.Streams)
                {
                    Console.Write ("Stream {0}: {1}", ++stream_count, stream.fccType);
                    if (stream.fccType == "auds")
                    {
                        string format;
                        switch (stream.Audio.wFormatTag)
                        {
                        default:
                            format = string.Format ("tag={0:X4}", stream.Audio.wFormatTag);
                            break;
                        case 0x0001:
                            format = "PCM";
                            break;
                        case 0x0055:
                            format = "MPEG Audio Layer 3";
                            break;
                        case 0x0161:
                        case 0x0162:
                        case 0x0163:
                            format = "WMA";
                            break;
                        case 0x2000:
                            format = "Dolby AC3";
                            break;
                        }
                        Console.Write (" [{0}] {1}kbps", format, stream.Audio.nAvgBytesPerSec*8/1000);
                        if (stream.Audio.nChannels == 1)
                            Console.Write (" Mono");
                        else if (stream.Audio.nChannels == 2)
                            Console.Write (" Stereo");
                        else
                            Console.Write (" {0}Ch", stream.Audio.nChannels);
                        Console.Write (" {0}Hz", stream.Audio.nSamplesPerSec);
                    }
                    else if (stream.fccType == "vids")
                    {
                        string format;
                        switch (stream.Video.biCompression)
                        {
                        default:
                            format = string.Format ("{0:X4}", stream.Video.biCompression);
                            break;
                        case 0x30355844:
                            format = "DivX 5";
                            break;
                        case 0x44495658:
                            format = "XviD";
                            break;
                        }
                        Console.Write (" [{0}] {1}x{2}", format, stream.Video.biWidth, stream.Video.biHeight);
                        Console.Write (" {0}kbps", (int)(info.VideoBitRate/1000));
                        double fps = 1e6/info.MicroSecPerFrame;
                        if ((int)(fps*100)%100 == 0)
                            Console.Write (" {0}FPS", (int)fps);
                        else if ((int)(fps*100)%10 == 0)
                            Console.Write (" {0:.0}FPS", fps);
                        else
                            Console.Write (" {0:.00}FPS", fps);
                    }
                    Console.WriteLine();
                }
                return 0;
            }
            catch (InvalidFormat X)
            {
                Console.Error.WriteLine ("AviFile error: {0}", X.Message);
                return 1;
            }
            catch (Exception X)
            {
                Console.Error.WriteLine (X.Message);
                return 1;
            }
        }
    }
}
