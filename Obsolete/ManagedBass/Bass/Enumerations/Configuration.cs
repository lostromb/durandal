﻿namespace ManagedBass
{
    enum Configuration
    {
        PlaybackBufferLength = 0,
        UpdatePeriod = 1,
        GlobalSampleVolume = 4,
        GlobalStreamVolume = 5,
        GlobalMusicVolume = 6,
        LogarithmicVolumeCurve = 7,
        LogarithmicPanCurve = 8,
        FloatDSP = 9,
        Algorithm3D = 10,
        NetTimeOut = 11,
        NetBufferLength = 12,
        PauseNoPlay = 13,
        NetPreBuffer = 15,
        NetAgent = 16,
        NetProxy = 17,
        NetPassive = 18,
        RecordingBufferLength = 19,
        NetPlaylist = 21,
        MusicVirtual = 22,
        FileVerificationBytes = 23,
        UpdateThreads = 24,
        DeviceBufferLength = 27,
        NoTimerResolution = 29,
        TruePlayPosition = 30,
        IOSMixAudio = 34,
        SuppressMP3ErrorCorruptionSilence = 35,
        IncludeDefaultDevice = 36,
        NetReadTimeOut = 37,
        VistaSpeakerAssignment = 38,
        IOSSpeaker = 39,
        MFDisable = 40,
        HandleCount = 41,
        UnicodeDeviceInformation = 42,
        SRCQuality = 43,
        SampleSRCQuality = 44,
        AsyncFileBufferLength = 45,
        IOSNotify = 46,
        OggPreScan = 47,
        MFVideo = 48,
        Airplay = 49,
        DevNonStop = 50,
        IOSNoCategory = 51,
        NetVerificationBytes = 52,

        // TODO: Implement config
        DevicePeriod = 53,
        Float = 54,
        AC3DynamicRangeCompression = 65537,
        WmaNetPreBuffer = 65793,
        WmaBassFileHandling = 65795,
        WmaNetSeek = 65796,
        WmaVideo = 65797,
        WmaAsync = 65807,
        CDFreeOld = 66048,
        CDRetry = 66049,
        CDAutoSpeed = 66050,
        CDSkipError = 66051,
        CDDBServer = 66052,
        EncodePriority = 66304,
        EncodeQueue = 66305,
        EncodeACMLoad = 66306,
        EncodeCastTimeout = 66320,
        EncodeCastProxy = 66321,
        MidiCompact = 66560,
        MidiVoices = 66561,
        MidiAutoFont = 66562,
        MidiDefaultFont = 66563,
        MidiInputPorts = 66564,
        MixerBufferLength = 67073,
        MixerPositionEx = 67074,
        SplitBufferLength = 67088,
        PlayAudioFromMp4 = 67328,
        AacSupportMp4 = 67329,
        DSDFrequency = 67584,
        WinampInputTimeout = 67584,
        DSDGain = 67585,

        ZXTuneMaxFileSize = unchecked((int)0xCF1D0100)
    }
}
