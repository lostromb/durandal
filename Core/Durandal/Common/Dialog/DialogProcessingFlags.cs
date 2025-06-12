using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog
{
    [Flags]
    public enum DialogFlag
    {
        None = 0x0,
        InMultiturnConversation = 0x1 << 1,
        UseEmptyConversationState = 0x1 << 2,
        IsCommonCarryoverIntent = 0x1 << 3,
        IsSideSpeech = 0x1 << 4,
        IsRetryEnabled = 0x1 << 5,
        IgnoringSideSpeech = 0x1 << 6,
        DivertToSideSpeechDomain = 0x1 << 7,
        BelowConfidence = 0x1 << 8,
        TenativeMultiturnEnabled = 0x1 << 9,
        NextTurnExternalDomain = 0x1 << 10,
        CrossDomainTriggered = 0x1 << 11,
    }

    public struct DialogProcessingFlags : IEquatable<DialogProcessingFlags>
    {
        private DialogFlag _internalFlags;
        
        public bool this[DialogFlag flag]
        {
            get
            {
                return _internalFlags.HasFlag(flag);
            }

            set
            {
                if (value)
                {
                    _internalFlags |= flag;
                }
                else
                {
                    _internalFlags &= ~flag;
                }
            }
        }

        public void Log(ILogger logger, LogLevel level)
        {
            var flags = _internalFlags;
            logger.Log(() => "Dialog processing flags: " + flags.ToString(), level);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DialogProcessingFlags))
            {
                return false;
            }

            DialogProcessingFlags other = (DialogProcessingFlags)obj;
            return Equals(other);
        }

        public bool Equals(DialogProcessingFlags other)
        {
            return _internalFlags == other._internalFlags;
        }

        public override int GetHashCode()
        {
            return 374742666 + _internalFlags.GetHashCode();
        }

        public static bool operator ==(DialogProcessingFlags left, DialogProcessingFlags right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DialogProcessingFlags left, DialogProcessingFlags right)
        {
            return !(left == right);
        }
    }
}
