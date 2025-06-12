package org.stromberg.durandal.net;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.UnsupportedEncodingException;
import java.net.SocketException;
import java.net.URLDecoder;
import java.net.URLEncoder;
import java.nio.charset.Charset;
import java.util.HashMap;
import java.util.Map;

/**
 *
 * @author lostromb
 */
public class DurandalHttpRequest
{
    public String RequestMethod = "GET";
    public String RequestFile = "/";
    public Map<String, String> RequestHeaders = new HashMap<String, String>();
    public byte[] PayloadData = new byte[0];
    public String ProtocolVersion = "HTTP/1.0";
    public Map<String, String> GetParameters = new HashMap<String, String>();
    
    public DurandalHttpRequest()
    {
    }

    public static DurandalHttpRequest readRequestFromStream(InputStream stream)
    {
        try
        {
            DurandalHttpRequest returnVal = new DurandalHttpRequest();

            HttpData data = HttpHelpers.readHttpData(stream);

            String[] allRequestLines = data.Headers.split("\r\n");
            if (allRequestLines.length == 0)
                return null;

            // Interpret the "GET /index.html HTTP/1.1" line
            String commandLine = allRequestLines[0];
            if (commandLine == null || commandLine.isEmpty())
                return null;

            String[] commandParts = commandLine.split(" ", 3);
            if (commandParts.length != 3)
                return null;

            returnVal.RequestMethod = commandParts[0];
            String entireRequestUri = URLDecoder.decode(commandParts[1], "UTF-8");
            returnVal.ProtocolVersion = commandParts[2];

            int parameterListStart = entireRequestUri.indexOf('?');
            if (parameterListStart < 0)
            {
                // No get parameters in the uri
                returnVal.RequestFile = entireRequestUri;
            }
            else
            {
                returnVal.RequestFile = entireRequestUri.substring(0, parameterListStart);
                String[] requestTokens = entireRequestUri.substring(parameterListStart + 1).split("&");

                // Parse get parameters from the URI
                for (int c = 0; c < requestTokens.length; c++)
                {
                    if (requestTokens[c] == null || requestTokens[c].isEmpty())
                        continue;
                    String[] parts = requestTokens[c].split("=");
                    if (parts.length != 2)
                        continue;
                    returnVal.GetParameters.put(parts[0].trim(), parts[1].trim());
                }
            }

            // Parse the headers
            for (int c = 1; c < allRequestLines.length; c++)
            {
                if (allRequestLines[c] == null || allRequestLines[c].isEmpty())
                    continue;
                String[] parts = allRequestLines[c].split(":");
                if (parts.length != 2)
                    continue;
                returnVal.RequestHeaders.put(parts[0].trim(), parts[1].trim());
            }

            // Copy the binary payload data
            returnVal.PayloadData = new byte[data.Payload.length];
            if (returnVal.PayloadData.length > 0)
                System.arraycopy(data.Payload, 0, returnVal.PayloadData, 0, returnVal.PayloadData.length);

            return returnVal;
        }
        catch (UnsupportedEncodingException e)
        {
            return null;
        }
        catch (IOException e)
        {
            return null;
        }
    }

    public boolean writeToStream(OutputStream stream)
    {
        try
        {
            StringBuilder headerBuilder = new StringBuilder();

            // Generate the content-length header
            int contentLength = PayloadData.length;
            if (contentLength != 0 || RequestMethod != "GET")
            {
                RequestHeaders.put("Content-Length", Integer.toString(contentLength));
            }

            String finalRequestUri = RequestFile;
            int getParametersInUri = finalRequestUri.contains("?") ? 1 : 0;
            for (String getParamKey : GetParameters.keySet())
            {
                if (getParametersInUri == 0)
                    finalRequestUri += String.format("?%s=%s", URLEncoder.encode(getParamKey, "UTF-8"), URLEncoder.encode(GetParameters.get(getParamKey), "UTF-8"));
                else
                    finalRequestUri += String.format("&%s=%s", URLEncoder.encode(getParamKey, "UTF-8"), URLEncoder.encode(GetParameters.get(getParamKey), "UTF-8"));
                getParametersInUri += 1;
            }

            if (finalRequestUri.length() > 4096)
            {
                System.err.println("Warning: Writing more than 4096 bytes to a request URL. This is disallowed in most browsers");
            }

            headerBuilder.append(String.format("%s %s %s\r\n", RequestMethod, finalRequestUri, ProtocolVersion));
            for (String headerKey : RequestHeaders.keySet())
            {
                headerBuilder.append(String.format("%s: %s\r\n", headerKey, RequestHeaders.get(headerKey)));
            }
            headerBuilder.append("\r\n");
            byte[] binary = headerBuilder.toString().getBytes(Charset.forName("UTF-8"));
            stream.write(binary);
            // Send the payload as well
            if (PayloadData.length > 0)
            {
                stream.write(PayloadData);
            }
            return true;
        }
        catch (UnsupportedEncodingException e)
        {
            return false;
        }
        catch (SocketException e)
        {
            return false;
        }
        catch (IOException e)
        {
            return false;
        }
    }

    public void setFormDataPayload(Map<String, String> postParameters)
    {
        StringBuilder builder = new StringBuilder();
        for (String key : postParameters.keySet())
        {
            if (builder.length() != 0)
            {
                builder.append("&");
            }
            try
            {
                builder.append(String.format("%s=%s", URLEncoder.encode(key, "UTF-8"), URLEncoder.encode(postParameters.get(key), "UTF-8")));
            }
            catch (UnsupportedEncodingException e)
            {
            }
        }
        byte[] data = builder.toString().getBytes(Charset.forName("UTF-8"));
        PayloadData = data;
        RequestHeaders.put("Content-Type", "application/x-www-form-urlencoded");
    }
    
    public Map<String, String> getFormDataFromPayload()
    {
        return HttpHelpers.getFormDataFromPayload(RequestHeaders, PayloadData);
    }
}
