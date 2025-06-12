namespace Durandal.Common.Client
{
    /// <summary>
    /// Static holder type for common "custom" fields set in client context -
    /// things like screen dimensions, form factor, supported versions, etc.
    /// </summary>
    public static class ClientContextField
    {
        public static readonly string FormFactor = "FormFactor";
        public static readonly string ClientType = "ClientType";
        public static readonly string ScreenWidth = "ScreenWidth";
        public static readonly string ScreenHeight = "ScreenHeight";
        public static readonly string ClientVersion = "ClientVersion";
        public static readonly string UserGivenName = "UserGivenName";
        public static readonly string UserFullName = "UserFullName";
        public static readonly string UserSurname = "UserSurname";
        public static readonly string UserEmail = "UserEmail";
        public static readonly string UserNickname = "UserNickname";
    }
}
