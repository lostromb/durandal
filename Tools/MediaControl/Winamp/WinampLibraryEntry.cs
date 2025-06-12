using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MediaControl.Winamp
{
    public class WinampLibraryEntry
    {
        public string FilePath;
        public string Title;
        public string Artist;
        public string Album;
        public string AlbumArtist;

        public bool ContainsSearchTerm(Regex term)
        {
            if (!string.IsNullOrEmpty(Title) && term.Match(Title).Success)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(Artist) && term.Match(Artist).Success)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(Album) && term.Match(Album).Success)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(AlbumArtist) && term.Match(AlbumArtist).Success)
            {
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}/{2} - {3}", Title, Artist, AlbumArtist, Album);
        }

        public override int GetHashCode()
        {
            if (FilePath == null)
            {
                return 0;
            }
            
            return FilePath.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            WinampLibraryEntry other = obj as WinampLibraryEntry;
            if (other == null)
            {
                return false;
            }

            if (FilePath == null)
            {
                if (other.FilePath == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return FilePath.Equals(other.FilePath);
        }
    }
}
