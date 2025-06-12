namespace Durandal.Extensions.Compression.Brotli
{
    using Durandal.Common.Dialog.Web;

    public class DialogBrotliJsonTransportProtocol : BrotliDialogProtocolWrapper
    {
        public DialogBrotliJsonTransportProtocol() : base(new DialogJsonTransportProtocol()) { }
    }
}
