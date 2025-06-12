package org.stromberg.durandal.client;

import com.microsoft.bond.BondSerializable;
import com.microsoft.bond.CompactBinaryReader;
import com.microsoft.bond.CompactBinaryWriter;
import com.microsoft.bond.ProtocolReader;
import com.microsoft.bond.ProtocolWriter;
import com.microsoft.bond.io.MemoryBondOutputStream;
import java.io.IOException;
import java.math.BigInteger;
import java.net.SocketException;
import java.nio.charset.Charset;
import java.util.Dictionary;
import java.util.HashMap;
import java.util.Map;
import org.json.simple.JSONObject;
import org.json.simple.parser.JSONParser;
import org.json.simple.parser.ParseException;
import org.stromberg.durandal.api.ClientRequest;
import org.stromberg.durandal.api.ClientResponse;
import org.stromberg.durandal.net.DurandalHttpRequest;
import org.stromberg.durandal.net.DurandalHttpResponse;
import org.stromberg.durandal.net.HttpClient;
import org.stromberg.durandal.security.ClientAuthenticator;
import org.stromberg.durandal.security.DurandalAuthentication;
import org.stromberg.durandal.security.PublicKey;

/**
 * This is the interface that sends a ClientRequest to a dialog HTTP endpoint and returns a ServerResponse
 * @author lostromb
 */
public class DialogHttpClient extends HttpClient
{
    public DialogHttpClient(String hostName, int remotePort)
    {
        super(hostName, remotePort);
    }

    public String getConnectionString()
    {
        return this.getServerAddress();
    }

    public JSONObject makeQueryRequest(JSONObject request)
    {
        try
        {
            String requestString = request.toJSONString();
            byte[] input = requestString.getBytes(Charset.forName("UTF-8"));
            byte[] result = sendRequest(input, "/query?format=json", 10000);
            if (result.length == 0)
                return null;
            JSONParser resultParser = new JSONParser();
            String responseString = new String(result, Charset.forName("UTF-8"));
            Object response = resultParser.parse(responseString);
            return (JSONObject)response;
        }
        catch (ParseException e)
        {
            System.err.println(e.getMessage());
            return null;
        }
    }
    
    public ClientResponse makeQueryRequest(ClientRequest request)
    {
        if (request == null)
        {
            return null;
        }

        byte[] input = serializeBond(request);
        if (input.length == 0)
        {
            return null;
        }

        byte[] result = sendRequest(input, "/query", 10000);
        if (result.length == 0)
        {
            return null;
        }

        ClientResponse response = new ClientResponse();
        if (deserializeBond(result, response))
        {
            return response;
        }
        
        return null;
    }
    
    public class AuthHelloResponse
    {
        public boolean Success;
        public boolean SecondTurnRequired;
    }
    
    public AuthHelloResponse makeAuthHelloRequest(ClientAuthenticator authenticator)
    {
        AuthHelloResponse returnVal = new AuthHelloResponse();
        System.out.println("Initializing client authentication");
        DurandalHttpRequest request = new DurandalHttpRequest();
        request.RequestMethod = "POST";
        request.RequestFile = "/auth_hello";
        Map<String, String> postParams = new HashMap<String,String>();
        postParams.put("clientId", authenticator.ClientId);
        postParams.put("clientName", authenticator.ClientName);
        PublicKey pubKey = authenticator.getPublicKey();
        postParams.put("E", DurandalAuthentication.serializeKey(pubKey.E));
        postParams.put("N", DurandalAuthentication.serializeKey(pubKey.N));
        request.setFormDataPayload(postParams);
        try
        {
            System.out.println("Sending auth_hello request... (ClientId = " + authenticator.ClientId + ")");
            DurandalHttpResponse response = sendRequest(request, 10000);
            if (response == null || response.ResponseCode != 200)
            {
                System.err.println("Request failed!");
                returnVal.SecondTurnRequired = false;
                returnVal.Success = false;
                return returnVal;
            }
            // Server has already verified you. Do one-pass authentication
            if (response.ResponseHeaders.containsKey("Authenticated") &&
                response.ResponseHeaders.get("Authenticated").equalsIgnoreCase("true"))
            {
                System.out.println("Request succeeded. Received encrypted shared secret");
                authenticator.decryptSharedSecret(new BigInteger(response.PayloadData));
                returnVal.SecondTurnRequired = false;
                returnVal.Success = true;
                return returnVal;
            }
            else
            {
                System.out.println("Request succeeded. Received challenge token");
                authenticator.storeChallengeToken(new BigInteger(response.PayloadData));
                returnVal.SecondTurnRequired = true;
                returnVal.Success = true;
                return returnVal;
            }
        }
        catch (Exception e)
        {
            // Could not connect to server.
            System.err.println("Could not connect to dialog server for auth hello request");
            System.err.println("Remote connection string is " + getConnectionString());
            System.err.println(e.getMessage());
            returnVal.SecondTurnRequired = false;
            returnVal.Success = false;
            return returnVal;
        }
    }

    public boolean makeAuthAnswerRequest(ClientAuthenticator authenticator)
    {
        System.out.println("Authenticating client step 2");
        DurandalHttpRequest request = new DurandalHttpRequest();
        request.RequestMethod = "POST";
        request.RequestFile = "/auth_answer";
        Map<String, String> postParams = new HashMap<String, String>();
        postParams.put("clientId", authenticator.ClientId);
        BigInteger challengeAnswer = authenticator.decryptChallengeToken();
        postParams.put("challengeAnswer", DurandalAuthentication.serializeKey(challengeAnswer));
        request.setFormDataPayload(postParams);
        try
        {
            System.out.println("Sending auth_answer request... (ClientId = " + authenticator.ClientId + ")");
            DurandalHttpResponse response = sendRequest(request, 10000);
            if (response == null || response.ResponseCode != 200)
            {
                System.err.println("Request failed!");
                return false;
            }
            System.out.println("Request succeeded");
            authenticator.decryptSharedSecret(new BigInteger(response.PayloadData));
            System.out.println("Client is now authenticated");
        }
        catch (Exception e)
        {
            // Could not connect to server.
            System.err.println("Could not connect to dialog server for auth answer request");
            System.err.println("Remote connection string is " + getConnectionString());
            System.err.println(e.getMessage());
            return false;
        }
        return true;
    }
    
    private static byte[] serializeBond(BondSerializable that)
    {
        try
        {
            MemoryBondOutputStream outStream = new MemoryBondOutputStream();
            ProtocolWriter writer = CompactBinaryWriter.createV1(outStream);
            that.write(writer);
            return outStream.toByteArray();
        }
        catch (IOException e)
        {
            System.err.println(e.getMessage());
            return new byte[0];
        }
    }
    
    private static boolean deserializeBond(byte[] that, BondSerializable container)
    {
        try
        {
            ProtocolReader reader = CompactBinaryReader.createV1(that);
            container.read(reader);
            return true;
        }
        catch (IOException e)
        {
            System.err.println(e.getMessage());
            return false;
        }
    }

    /*public ClientResponse MakeViewRequest(ClientRequest request)
    {
        try
        {
            byte[] result = SendRequest(BondConverter.Serialize(request), "/query");
            if (result.Length == 0)
                return null;
            ClientResponse dialogResult = BondConverter.Deserialize<ClientResponse>(result);
            return dialogResult;
        }
        catch (SocketException e)
        {
            // Could not connect to server.
            DataLogger.LogError(e.Message);
            return null;
        }
    }*/

    public boolean resetConversationState(String clientId)
    {
        DurandalHttpRequest request = new DurandalHttpRequest();
        request.RequestMethod = "POST";
        request.RequestFile = "/reset";
        request.ProtocolVersion = "HTTP/1.0";
        request.GetParameters.put("clientid", clientId);
        DurandalHttpResponse response = sendRequest(request, 10000);
        if (response == null || response.ResponseCode != 200)
            return false;
        return true;
    }

    /*public ClientResponse MakeDialogActionRequest(ClientRequest request, string url)
    {
        try
        {
            byte[] result = SendRequest(BondConverter.Serialize(request), url);
            if (result.Length == 0)
                return null;
            ClientResponse durandalResult = BondConverter.Deserialize<ClientResponse>(result);
            return durandalResult;
        }
        catch (SocketException e)
        {
            // Could not connect to server.
            DataLogger.LogError(e.Message);
            return null;
        }
    }*/
}
