//! \file       Sound.cs
//! \date       Wed Aug 10 00:55:16 2011
//! \brief      PlaySound wrapper.
//

using System;
using System.Runtime.InteropServices;

namespace Rnd.Windows
{
    class WinMM
    {
        [DllImport("winmm.dll", EntryPoint="PlaySoundW", CharSet=CharSet.Unicode, SetLastError=true)]
        public static extern bool PlaySound (string pszSound, UIntPtr hmod, uint fdwSound);

        [DllImport("winmm.dll", EntryPoint="PlaySoundW", CharSet=CharSet.Unicode, SetLastError=true)]
        public static extern bool PlaySoundAlias (UIntPtr pszSound, UIntPtr hmod, uint fdwSound);

        public static bool PlayAlias (SoundAlias alias, SoundFlags flags = SoundFlags.Sync)
        {
            return PlaySoundAlias ((UIntPtr)alias,  UIntPtr.Zero, (uint)(SoundFlags.AliasID | flags));
        }

        public enum SoundAlias
        {
            SystemAsterisk      = 'S' | ('*' << 8),
            SystemDefault       = 'S' | ('D' << 8),
            SystemExclamation   = 'S' | ('!' << 8),
            SystemExit          = 'S' | ('E' << 8),
            SystemHand          = 'S' | ('H' << 8),
            SystemQuestion      = 'S' | ('?' << 8),
            SystemStart         = 'S' | ('S' << 8),
            SystemWelcome       = 'S' | ('W' << 8),
        }

        [Flags]
        public enum SoundFlags
        {
            /// <summary>play synchronously (default)</summary>
            Sync = 0x0000,    
            /// <summary>play asynchronously</summary>
            Async = 0x0001,
            /// <summary>silence (!default) if sound not found</summary>
            NoDefault = 0x0002,
            /// <summary>pszSound points to a memory file</summary>
            Memory = 0x0004,
            /// <summary>loop the sound until next sndPlaySound</summary>
            Loop = 0x0008,    
            /// <summary>don't stop any currently playing sound</summary>
            NoStop = 0x0010,
            /// <summary>Stop Playing Wave</summary>
            Purge = 0x40,
            /// <summary>don't wait if the driver is busy</summary>
            NoWait = 0x00002000,
            /// <summary>name is a registry alias</summary>
            Alias = 0x00010000,
            /// <summary>alias is a predefined id</summary>
            AliasID = 0x00110000,
            /// <summary>name is file name</summary>
            Filename = 0x00020000,
            /// <summary>name is resource name or atom</summary>
            Resource = 0x00040004
        }
    }
}
