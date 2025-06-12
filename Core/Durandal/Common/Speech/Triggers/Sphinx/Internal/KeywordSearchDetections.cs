using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class KeywordSearchDetections
    {
        internal static void kws_detections_reset(kws_detections_t detections)
        {
            if (detections.detect_list.IsNull)
                return;

            detections.detect_list = PointerHelpers.NULL<gnode_t>();
        }

        internal static void kws_detections_add(kws_detections_t detections, Pointer<byte> keyphrase, int sf, int ef, int prob, int ascr)
        {
            Pointer<gnode_t> gn;
            kws_detection_t detection;
            for (gn = detections.detect_list; gn.IsNonNull; gn = GenericList.gnode_next(gn))
            {
                kws_detection_t det = (kws_detection_t)GenericList.gnode_ptr(gn);
                if (cstring.strcmp(keyphrase, det.keyphrase) == 0 && det.sf < ef && det.ef > sf)
                {
                    if (det.prob < prob)
                    {
                        det.sf = sf;
                        det.ef = ef;
                        det.prob = prob;
                        det.ascr = ascr;
                    }
                    return;
                }
            }

            /* Nothing found */
            detection = new kws_detection_t();
            detection.sf = sf;
            detection.ef = ef;
            detection.keyphrase = keyphrase;
            detection.prob = prob;
            detection.ascr = ascr;
            detections.detect_list = GenericList.glist_add_ptr(detections.detect_list, detection);
        }

        internal static Pointer<byte> kws_detections_hyp_str(kws_detections_t detections, int frame, int delay)
        {
            Pointer<gnode_t> gn;
            Pointer<byte> c;
            int len;
            Pointer<byte> hyp_str;

            len = 0;
            for (gn = detections.detect_list; gn.IsNonNull; gn = GenericList.gnode_next(gn))
            {
                kws_detection_t det = (kws_detection_t)GenericList.gnode_ptr(gn);
                if (det.ef < frame - delay)
                {
                    len += (int)cstring.strlen(det.keyphrase) + 1;
                }
            }

            if (len == 0)
            {
                return PointerHelpers.NULL<byte>();
            }

            hyp_str = CKDAlloc.ckd_calloc<byte>(len);
            c = hyp_str;
            bool cMoved = false; // LOGAN modified
            for (gn = detections.detect_list; gn.IsNonNull; gn = GenericList.gnode_next(gn))
            {
                kws_detection_t det = (kws_detection_t)GenericList.gnode_ptr(gn);
                if (det.ef < frame - delay)
                {
                    det.keyphrase.MemCopyTo(c, (int)cstring.strlen(det.keyphrase));
                    c += cstring.strlen(det.keyphrase);
                    c.Deref = (byte)' ';
                    c++;
                    cMoved = true;
                }
            }

            if (cMoved)
            {
                c--;
                c.Deref = (byte)'\0';
            }

            return hyp_str;
        }
    }
}
