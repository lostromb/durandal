
namespace Durandal.Common.Net.Http2.HPack
{
    /// <summary>
    /// An entry in the static or dynamic table
    /// </summary>
    public struct TableEntry
    {
        public string Name;
        public int NameLen;
        public string Value;
        public int ValueLen;
    }
}