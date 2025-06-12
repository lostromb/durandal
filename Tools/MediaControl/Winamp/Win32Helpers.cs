namespace MediaControl.Winamp
{
    using System;
    using System.Runtime.InteropServices;

    public class Win32Helpers
    {
        #region Win32 constants
        public const int WM_COMMAND = 0x111; //273;
        public const int WM_USER = 0x0400;
        public const int WM_COPYDATA = 0x4a;

        #endregion

        #region Win23 structs

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpData;
        }

        /*[Serializable, StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left_, int top_, int right_, int bottom_)
            {
                Left = left_;
                Top = top_;
                Right = right_;
                Bottom = bottom_;
            }

            public int Height { get { return Bottom - Top + 1; } }
            public int Width { get { return Right - Left + 1; } }
            public Size Size { get { return new Size(Width, Height); } }

            public Point Location { get { return new Point(Left, Top); } }

            // Handy method for converting to a System.Drawing.Rectangle
            public Rectangle ToRectangle()
            { return Rectangle.FromLTRB(Left, Top, Right, Bottom); }

            public static RECT FromRectangle(Rectangle rectangle)
            {
                return new RECT(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
            }

            public override int GetHashCode()
            {
                return Left ^ ((Top << 13) | (Top >> 0x13))
                  ^ ((Width << 0x1a) | (Width >> 6))
                  ^ ((Height << 7) | (Height >> 0x19));
            }

            #region Operator overloads

            public static implicit operator Rectangle(RECT rect)
            {
                return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }

            public static implicit operator RECT(Rectangle rect)
            {
                return new RECT(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }

            #endregion
        }*/

        #endregion

        #region Win32 function imports

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(
        [MarshalAs(UnmanagedType.LPTStr)] string lpClassName,
        [MarshalAs(UnmanagedType.LPTStr)] string lpWindowName
        );

        /*[DllImport("User32.dll")]
        public static extern int FindWindow(string strClassName, 
                                                string strWindowName);*/

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessage(
        IntPtr hwnd,
        int wMsg,
        int wParam,
        uint lParam
        );

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessageA(IntPtr hwnd, int wMsg, int wParam,
            [In()] ref COPYDATASTRUCT lParam);

        //[DllImport("shell32.dll", CharSet = CharSet.Auto)]
        //public static extern int ShellExecute(
        #endregion

    }

}
