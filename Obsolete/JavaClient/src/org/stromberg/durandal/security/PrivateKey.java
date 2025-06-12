package org.stromberg.durandal.security;

import java.io.File;
import java.io.IOException;
import java.io.PrintWriter;
import java.math.BigInteger;
import java.util.Scanner;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 *Represents a private key in the RSA schema (the D value is the secret key)
 * @author Logan Stromberg
 */
public class PrivateKey
{
    /// <summary>
    /// The secret key value
    /// </summary>
    public BigInteger D;

    /// <summary>
    /// The public key exponent
    /// </summary>
    public BigInteger E;

    /// <summary>
    /// The public key modulus
    /// </summary>
    public BigInteger N;

    /// <summary>
    /// Creates a private key with the specified D, E, and Modulus values
    /// </summary>
    /// <param name="d">The secret exponent</param>
    /// <param name="e">The public exponent (usually 65537)</param>
    /// <param name="n">The public modulus value</param>
    public PrivateKey(BigInteger d, BigInteger e, BigInteger n)
    {
        D = d;
        E = e;
        N = n;
    }

    /// <summary>
    /// Exports the public parameters of this key as a PublicKey object
    /// </summary>
    /// <returns></returns>
    public PublicKey getPublicKey()
    {
        return new PublicKey(E, N);
    }

    /// <summary>
    /// Raw encryption method. Only a private key can execute this. The input value must be lower than the modulo value N
    /// </summary>
    /// <param name="value"></param>
    /// <returns>VALUE ^ D % N</returns>
    public BigInteger encrypt(BigInteger value)
    {
        return value.modPow(D, N);
    }

    /// <summary>
    /// Raw decryption method. Anyone with the public key can perform this operation.
    /// </summary>
    /// <param name="value"></param>
    /// <returns>VALUE ^ E % N</returns>
    public BigInteger decrypt(BigInteger value)
    {
        return value.modPow(E, N);
    }

    public String writeToXml(BigInteger requestTokenBase)
    {
        return "<rsa_private_key D=\"" + DurandalAuthentication.serializeKey(D) + "\" E=\"" + DurandalAuthentication.serializeKey(E) + "\" N=\"" + DurandalAuthentication.serializeKey(N) + "\" T=\"" + DurandalAuthentication.serializeKey(requestTokenBase) + "\" />";
    }

     public boolean writeToFile(String fileName, BigInteger requestTokenBase)
    {
        try
        {
            PrintWriter writer = new PrintWriter(fileName);
            String xml = writeToXml(requestTokenBase);
            writer.write(xml);
            writer.close();
            return true;
        }
        catch (IOException e)
        {
            System.err.println("Error while writing private key to file");
            System.err.println(e.getMessage());
        }
        return false;
    }

    public static PrivateKeyWithToken readFromXmlString(String xml)
    {
        BigInteger D = null;
        BigInteger E = null;
        BigInteger N = null;
        BigInteger T = BigInteger.ZERO;
        
        // Super hackish xml attribute parsing
        Pattern attributeParser = Pattern.compile("([a-zA-Z]+)=\"(.+?)\"");
        Matcher m = attributeParser.matcher(xml);
        while (m.find())
        {
            if (m.group(1).equals("D"))
            {
                D = DurandalAuthentication.deserializeKey(m.group(2));
            }
            else if (m.group(1).equals("E"))
            {
                E = DurandalAuthentication.deserializeKey(m.group(2));
            }
            else if (m.group(1).equals("N"))
            {
                N = DurandalAuthentication.deserializeKey(m.group(2));
            }
            else if (m.group(1).equals("T"))
            {
                T = DurandalAuthentication.deserializeKey(m.group(2));
            }
        }
        
        if (D == null || E == null || N == null)
        {
            return null;
        }
        
        PrivateKeyWithToken returnVal = new PrivateKeyWithToken();
        returnVal.Key = new PrivateKey(D, E, N);
        returnVal.RequestTokenBase = T;
        return returnVal;
    }

    public static PrivateKeyWithToken readFromFile(String fileName)
    {
        try
        {
            StringBuilder file = new StringBuilder();
            Scanner input = new Scanner(new File(fileName));
            while (input.hasNextLine())
            {
                file.append(input.nextLine());
            }
            input.close();
            return readFromXmlString(file.toString());
        }
        catch (IOException e)
        {
            System.err.println(e.getMessage());
            return null;
        }
    }
    
    public static class PrivateKeyWithToken
    {
        public PrivateKey Key;
        public BigInteger RequestTokenBase;
    }
}
