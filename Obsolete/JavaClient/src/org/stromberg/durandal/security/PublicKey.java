package org.stromberg.durandal.security;

import java.io.IOException;
import java.io.PrintWriter;
import java.math.BigInteger;
import java.util.Scanner;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 *Represents a public key in the RSA schema
 * @author Logan Stromberg
 */
public class PublicKey
{
    /// <summary>
    /// The public exponent (usually 65537)
    /// </summary>
    public BigInteger E;

    /// <summary>
    /// The modulus value
    /// </summary>
    public BigInteger N;

    /// <summary>
    /// Creates a new public key with the specified exponent and modulus values
    /// </summary>
    /// <param name="e"></param>
    /// <param name="n"></param>
    public PublicKey(BigInteger e, BigInteger n)
    {
        E = e;
        N = n;
    }

    /// <summary>
    /// Performs raw decryption on the input value, for use in signing/verifying a client against their private key.
    /// </summary>
    /// <param name="value"></param>
    /// <returns>VALUE ^ E % N</returns>
    public BigInteger signVerify(BigInteger value)
    {
        return value.modPow(E, N);
    }

    public String writeToXml(BigInteger requestTokenBase)
    {
        return "<rsa_public_key E=\"" + DurandalAuthentication.serializeKey(E) + "\" N=\"" + DurandalAuthentication.serializeKey(N) + "\" />";
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
            System.err.println("Error while writing public key to file");
            System.err.println(e.getMessage());
        }
        return false;
    }

    public static PublicKey readFromXmlString(String xml)
    {
        BigInteger E = null;
        BigInteger N = null;
        
        // Super hackish xml attribute parsing
        Pattern attributeParser = Pattern.compile("([a-zA-Z]+)=\"(.+?)\"");
        Matcher m = attributeParser.matcher(xml);
        while (m.find())
        {
            if (m.group(1).equals("E"))
            {
                E = DurandalAuthentication.deserializeKey(m.group(2));
            }
            else if (m.group(1).equals("N"))
            {
                N = DurandalAuthentication.deserializeKey(m.group(2));
            }
        }
        
        if (E == null || N == null)
        {
            return null;
        }
        
        PublicKey returnVal = new PublicKey(E, N);
        return returnVal;
    }

    public static PublicKey readFromFile(String fileName)
    {
        StringBuilder file = new StringBuilder();
        Scanner input = new Scanner(fileName);
        while (input.hasNextLine())
        {
            file.append(input.nextLine());
        }
        input.close();
        return readFromXmlString(file.toString());
    }
}

