using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class PackagedIO
    {
        private const int COMP_NONE = 0;
        private const int COMP_COMPRESS = 1;
        private const int COMP_GZIP = 2;
        private const int COMP_BZIP2 = 3;

        private const int FREAD_RETRY_COUNT = 60;
        private const int STAT_RETRY_COUNT = 10;

        internal static lineiter_t lineiter_start(FILE fh)
        {
            lineiter_t li;

            li = new lineiter_t();
            li.buf = CKDAlloc.ckd_malloc<byte>(128);
            li.buf[0] = (byte)'\0';
            li.bsiz = 128;
            li.len = 0;
            li.fh = fh;

            li = lineiter_next(li);

            /* Strip the UTF-8 BOM */

            if (li != null && 0 == cstring.strncmp(li.buf, cstring.ToCString("\xef\xbb\xbf"), 3))
            {
                li.buf.Point(3).MemMove(-3, (int)cstring.strlen(li.buf + 1));
                li.len -= 3;
            }

            return li;
        }

        internal static lineiter_t lineiter_start_clean(FILE fh)
        {
            lineiter_t li;

            li = lineiter_start(fh);

            if (li == null)
                return li;

            li.clean = 1;

            if (li.buf.IsNonNull && li.buf[0] == '#')
            {
                li = lineiter_next(li);
            }
            else
            {
                StringFuncs.string_trim(li.buf, string_edge_e.STRING_BOTH);
            }

            return li;
        }

        internal static lineiter_t lineiter_next_plain(lineiter_t li)
        {
            /* We are reading the next line */
            li.lineno++;

            /* Read a line and check for EOF. */
            if (li.fh.fgets(li.buf, li.bsiz).IsNull)
            {
                return null;
            }

            /* If we managed to read the whole thing, then we are done
             * (this will be by far the most common result). */
            li.len = (int)cstring.strlen(li.buf);
            if (li.len < li.bsiz - 1 || li.buf[li.len - 1] == '\n')
                return li;

            /* Otherwise we have to reallocate and keep going. */
            while (true)
            {
                li.bsiz *= 2;
                li.buf = CKDAlloc.ckd_realloc(li.buf, (uint)li.bsiz);
                /* If we get an EOF, we are obviously done. */
                if (li.fh.fgets(li.buf + li.len, li.bsiz - li.len).IsNull)
                {
                    li.len += (int)cstring.strlen(li.buf + li.len);
                    return li;
                }
                li.len += (int)cstring.strlen(li.buf + li.len);
                /* If we managed to read the whole thing, then we are done. */
                if (li.len < li.bsiz - 1 || li.buf[li.len - 1] == '\n')
                    return li;
            }
        }

        internal static lineiter_t lineiter_next(lineiter_t li)
        {
            if (li.clean == 0)
                return lineiter_next_plain(li);

            for (li = lineiter_next_plain(li); li != null; li = lineiter_next_plain(li))
            {
                if (li.buf.IsNonNull)
                {
                    li.buf = StringFuncs.string_trim(li.buf, string_edge_e.STRING_BOTH);
                    if (li.buf[0] != 0 && li.buf[0] != '#')
                        break;
                }
            }
            return li;
        }
    }
}
