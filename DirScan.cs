//! \file       DirScan.cs
//! \date       Sun Aug 21 06:35:21 2011
//! \brief      ScanDirectory class performs recursive directory traversal.
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

namespace Rnd.Shell
{
    /// <summary>
    /// Scan directory and its subdirecories for files matching specified
    /// criteria.
    /// </summary>

    public class ScanDirectory
    {
        /// <summary>
        /// delegate that matches supplied filename against some criteria
        /// </summary>
        /// <returns>Matching result.</returns>
        public delegate bool MatchFile (string filename);

        /// <summary>
        /// delegate that watches over scan progress.
        /// </summary>
        /// <param name="count">Number of files found so far</param>
        /// <param name="path">Either name of the directory being entered,
        /// or filename matched</param>
        /// <param name="state">Describes path <see cref="MatchState"/></param>
        /// <returns>True if scan could be continued,
        /// False if scan should be aborted</returns>
        public delegate bool ProgressCallback (int count, string path, MatchState state);

        private List<string> lstFilesFound = new List<string>();

        public MatchFile        Matcher     { get; set; }
        public ProgressCallback Callback    { get; set; }
        public IList<string>    FilesFound  { get { return lstFilesFound; } }

        public ScanDirectory ()
        {
            Matcher = f => true;
        }

        public ScanDirectory (MatchFile matcher)
        {
            Matcher = matcher;
        }

        public ScanDirectory (MatchFile matcher, ProgressCallback callback)
        {
            Matcher = matcher;
            Callback = callback;
        }

        public class SearchCancelled : Exception
        {
            public SearchCancelled () : base() { }
        }

        /// <summary>
        /// internal count for files and directories scanned so far.
        /// </summary>
        private int count = 0;

        /// <summary>
        /// States supplied to callback delegate.
        /// </summary>
        public enum MatchState
        {
            /// <summary>Filename did not match criteria.</summary>
            NoMatch,
            /// <summary>Filename matched criteria.</summary>
            Matched,
            /// <summary>Filename is a directory being descended into.</summary>
            EnterDirectory,
        };

        /// <summary>
        /// Recirsively scan directory for files matching specific criteria.
        /// </summary>
        /// <exception cref="SearchCancelled">Thrown if search was cancelled
        /// by Callback</exception>

        private void DirSearch (string dir) 
        {
            try	
            {
                foreach (string d in Directory.GetDirectories (dir)) 
                {
                    if (Callback != null && !Callback (count, d, MatchState.EnterDirectory))
                        throw new SearchCancelled();

                    DirSearch(d);
                }
                foreach (string f in Directory.GetFiles (dir))
                {
                    bool match = Matcher (f);
                    if (match)
                    {
                        ++count;
                        lstFilesFound.Add(f);
                    }
                    if (Callback != null && !Callback (count, f,
                                match?  MatchState.Matched: MatchState.NoMatch))
                        throw new SearchCancelled();
                }
            }
            catch (SystemException)
            {
                // if IO exception occurs (eg access violation)
                // it is ignored, and search continues in the next directory.
            }
        }

        public List<string> Scan (string dir)
        {
            count = 0;
            lstFilesFound.Clear();
            DirSearch (dir);
            return lstFilesFound;
        }

        public List<string> ScanList (IEnumerable<string> dir_list)
        {
            count = 0;
            lstFilesFound.Clear();

            if (dir_list != null)
                foreach (var dir in dir_list)
                    DirSearch (dir);

            return lstFilesFound;
        }
    }
}
