package org.stromberg.durandal.security;

import java.io.File;
import java.math.BigInteger;
import org.stromberg.durandal.api.SecurityToken;

/**
 *A self-contained component for a client to manage its private key and authenticate itself with a Durandal service.
 * @author Logan Stromberg
 */
public class ClientAuthenticator
{
    private PrivateKey _key;
    private BigInteger _sharedSecretToken;
    private BigInteger _challengeToken;
    private BigInteger _requestTokenBase;

    public String ClientId;
    public String ClientName;

    public ClientAuthenticator(String clientId, String clientName)
    {
        //_logger = logger;
        ClientId = clientId;
        ClientName = clientName;
    }

    public PublicKey getPublicKey()
    {
        return _key.getPublicKey();
    }

    public boolean loadPrivateKeyFromFile(String fileName)
    {
        File file = new File(fileName);
        if (!file.exists())
        {
            return false;
        }
        try
        {
            PrivateKey.PrivateKeyWithToken keyWithToken = PrivateKey.readFromFile(fileName);
            if (keyWithToken == null)
            {
                return false;
            }
            
            _key = keyWithToken.Key;
            _requestTokenBase = keyWithToken.RequestTokenBase;
            System.out.println("Loaded private key information from " + fileName);
            return true;
        }
        catch (Exception e)
        {
            System.err.println("Could not deserialize client private key from file " + fileName);
        }
        return false;
    }

    public boolean savePrivateKeyToFile(String fileName)
    {
        try
        {
            _requestTokenBase = _requestTokenBase.add(BigInteger.ONE);
            boolean returnVal = _key.writeToFile(fileName, _requestTokenBase);
            if (returnVal)
            {
                System.out.println("Wrote private key information to " + fileName);
            }
            else
            {
                System.err.println("Error occurred while writing private key information to " + fileName);
            }
            return returnVal;
        }
        catch (Exception e)
        {
            System.err.println("Could not serialize client private key to file " + fileName);
        }
        return false;
    }

    public void storeChallengeToken(BigInteger challengeToken)
    {
        _challengeToken = challengeToken;
    }

    public BigInteger decryptChallengeToken()
    {
        return _key.encrypt(_challengeToken);
    }

    public void decryptSharedSecret(BigInteger encryptedSecret)
    {
        _sharedSecretToken = _key.encrypt(encryptedSecret);
    }

    public RequestToken generateUniqueRequestToken()
    {
        if (_sharedSecretToken == null)
        {
            //_logger.Log("Security token was not generated because SharedSecret = null (did the client perform a handshake?)", LogLevel.Wrn);
            return null;
        }
        BigInteger tokenRed = _requestTokenBase;
        BigInteger tokenBlue = _key.encrypt(tokenRed.xor(_sharedSecretToken));
        _requestTokenBase.add(BigInteger.ONE);
        return new RequestToken(tokenRed, tokenBlue);
    }

    /// <summary>
    /// Creats a valid authentication token to be added to a client request
    /// </summary>
    public SecurityToken generateAuthToken()
    {
        if (_sharedSecretToken == null)
        {
            //_logger.Log("Security token was not generated because SharedSecret = null (did the client perform a handshake?)", LogLevel.Wrn);
            return null;
        }
        RequestToken authToken = generateUniqueRequestToken();
        SecurityToken returnVal = new SecurityToken();
        returnVal.setRed(DurandalAuthentication.serializeKey(authToken.TokenRed));
        returnVal.setBlue(DurandalAuthentication.serializeKey(authToken.TokenBlue));
        return returnVal;
    }
}

