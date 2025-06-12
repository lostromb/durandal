namespace Durandal.Common.Security
{
    using Durandal.API;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;

    /// <summary>
    /// A unique token that is used to sign/verify individual requests to the service.
    /// The "Red" token is the "counter", it is a semi-random public value containing an expiration time for the request
    /// The "Blue" token is equal to Sign(Red XOR Secret). The server will verify this value using
    /// its stored public key, and ensure it is equal to the red token xor'd with a shared secret value.
    /// Request tokens usually have a very short life (less than 30 seconds) before their expire time passes;
    /// this is to prevent replay attacks.
    /// </summary>
    public class RequestToken
    {
        /// <summary>
        /// The red component of the token (the public seed value)
        /// </summary>
        public BigInteger TokenRed;

        /// <summary>
        /// The blue component of the token (the signed seed)
        /// </summary>
        public BigInteger TokenBlue;

        /// <summary>
        /// Creates a new request token
        /// </summary>
        /// <param name="red"></param>
        /// <param name="blue"></param>
        public RequestToken(BigInteger red, BigInteger blue)
        {
            TokenRed = red;
            TokenBlue = blue;
        }

        /// <summary>
        /// Creates a request token based on serialized hex strings
        /// </summary>
        /// <param name="red"></param>
        /// <param name="blue"></param>
        public RequestToken(string red, string blue)
        {
            TokenRed = CryptographyHelpers.DeserializeKey(red);
            TokenBlue = CryptographyHelpers.DeserializeKey(blue);
        }

        /// <summary>
        /// Creates a request token based on raw byte sequences
        /// </summary>
        /// <param name="red"></param>
        /// <param name="blue"></param>
        public RequestToken(byte[] red, byte[] blue)
        {
            TokenRed = new BigInteger(red);
            TokenBlue = new BigInteger(blue);
        }
    }
}
