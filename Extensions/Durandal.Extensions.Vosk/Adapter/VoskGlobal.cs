namespace Durandal.Extensions.Vosk.Adapter
{

    internal class VoskGlobal
    {
        public static void SetLogLevel(int level)
        {
            VoskPInvoke.SetLogLevel(level);
        }

        public static void GpuInit()
        {
            VoskPInvoke.GpuInit();
        }

        public static void GpuThreadInit()
        {
            VoskPInvoke.GpuThreadInit();
        }
    }

}