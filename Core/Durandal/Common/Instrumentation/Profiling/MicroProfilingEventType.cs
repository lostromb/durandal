using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation.Profiling
{
    public enum MicroProfilingEventType : ushort
    {
        // Common or meta events
        Unknown = 0,
        KeepAlive = 1,
        UnitTest = 2,

        // MMIO read
        MMIO_Read_EnterReadAnyMethod = 100,
        MMIO_Read_PreRead = 101,
        MMIO_Read_Spinwait_Start = 102,
        MMIO_Read_Spinwait_End = 103,
        MMIO_Read_ReadStart = 104,
        MMIO_Read_ReadDataFinish = 105,
        MMIO_Read_ReadFinish = 106,
        MMIO_Read_Single_Spinwait_Start = 107,
        MMIO_Read_Single_Spinwait_End = 108,

        // MMIO write
        MMIO_Write_EnterWriteMethod = 200,
        MMIO_Write_PreWrite = 201,
        MMIO_Write_StallBufferFull = 202,
        MMIO_Write_WriteStart = 203,
        MMIO_Write_WriteDataFinish = 204,
        MMIO_Write_WriteFinish = 205,

        KeepAlive_Ping_SendRequestStart = 300,
        KeepAlive_Ping_SendRequestFinish = 301,
        KeepAlive_Ping_RecvRequestStart = 302,
        KeepAlive_Ping_RecvRequestFinish = 303,
        KeepAlive_Ping_SendResponseStart = 304,
        KeepAlive_Ping_SendResponseFinish = 305,
        KeepAlive_Ping_RecvResponseStart = 306,
        KeepAlive_Ping_RecvResponseFinish = 307,

        // Etc.
        Bug_Repro_Trigger_Stutter = 3000
    }
}
