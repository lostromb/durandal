package org.stromberg.durandal.security;

import java.math.BigInteger;
import java.util.Random;

/**
 * Provides common cryptography functions for Durandal client/server authenticators
 * @author Logan Stromberg
 */
public class DurandalAuthentication
{
    private static final int RSA_KEY_SIZE = 512;
    private static final int SHARED_TOKEN_SIZE = 512;
    
    /**
     * Generates a new 512-bit private RSA key
     * @return 
     */
    public static PrivateKey generateRSAKey()
    {
        /*try
        {
            KeyPairGenerator keyPairGenerator = KeyPairGenerator.getInstance("RSA");
            keyPairGenerator.initialize(RSA_KEY_SIZE);
            KeyPair keyPair = keyPairGenerator.genKeyPair();
            RSAPrivateCrtKeyImpl key = (RSAPrivateCrtKeyImpl)keyPair.getPrivate();
            return new PrivateKey(key.getPrivateExponent(), key.getPublicExponent(), key.getModulus());
        }
        catch (NoSuchAlgorithmException e) {}
        return null;*/
        
        BigInteger t, p, q, n, e, d;
        Random random = new Random();
        PrivateKey returnVal = null;
        while (returnVal == null)
        {
            try
            {
                do
                {
                    p = BigInteger.probablePrime(RSA_KEY_SIZE / 2, random);
                    q = BigInteger.probablePrime(RSA_KEY_SIZE / 2, random);
                    n = p.multiply(q);
                    e = BigInteger.valueOf(65535);
                    t = p.subtract(BigInteger.ONE).multiply(q.subtract(BigInteger.ONE));
                }
                while (!t.gcd(e).equals(BigInteger.ONE));
                d = e.modInverse(t);
                returnVal = new PrivateKey(d, e, n);
            }
            catch (ArithmeticException exception) {} // this can occur if no multiplicative inverse exists. In this case, try the algorithm again
        }
        
        return returnVal;
    }

    /// <summary>
    /// Generates a random large number, with the specified maximum value and bit length (default 512 bits)
    /// </summary>
    /// <param name="bitLength"></param>
    /// <returns></returns>
    public static BigInteger generateRandomToken(BigInteger maxValue, int bitLength)
    {
        BigInteger candidate;
        Random rand = new Random();
        do
        {
            byte[] data = new byte[Math.max(1, bitLength / 8)];
            rand.nextBytes(data);
            for (int c = 0; c < data.length; c++)
            {
                // Make sure all bytes are nonzero
                while (data[c] == 0)
                {
                    data[c] = (byte)rand.nextInt();
                }
            }
            candidate = new BigInteger(data);
        } while (candidate.compareTo(maxValue) >= 0);
        return candidate;
    }
    
    public static BigInteger generateRandomToken(BigInteger maxValue)
    {
        return generateRandomToken(maxValue, SHARED_TOKEN_SIZE);
    }

    /// <summary>
    /// Serializes a BigInteger value into a hex string ("51A0F4b3")
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static String serializeKey(BigInteger key)
    {
        return key.toString(16);
    }

    /// <summary>
    /// Deserializes a BigInteger value from a hex string ("51A0F4b3")
    /// If parsing fails, this method will throw an ArithmeticException
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static BigInteger deserializeKey(String key)
    {
        return new BigInteger(key, 16);
    }
}
