package org.stromberg.durandal.net;

import java.io.Console;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.UnsupportedEncodingException;
import java.net.URLEncoder;
import java.nio.charset.Charset;
import java.util.HashMap;
import java.util.Map;

/**
 *
 * @author lostromb
 */
public class DurandalHttpResponse
{
    public int ResponseCode = 0;
    public String ResponseMessage = "";
    public Map<String, String> ResponseHeaders = new HashMap<String, String>();
    public byte[] PayloadData = new byte[0];
    public String ProtocolVersion = "HTTP/1.0";
    
    public DurandalHttpResponse()
    {
    }

    public static DurandalHttpResponse readResponseFromStream(InputStream stream)
    {
        try
        {
            DurandalHttpResponse returnVal = new DurandalHttpResponse();

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

            returnVal.ProtocolVersion = commandParts[0];
            returnVal.ResponseCode = Integer.parseInt(commandParts[1]);
            returnVal.ResponseMessage = commandParts[2];

            // Parse the headers
            for (int c = 1; c < allRequestLines.length; c++)
            {
                if (allRequestLines[c] == null || allRequestLines[c].isEmpty())
                    continue;
                String[] parts = allRequestLines[c].split(":");
                if (parts.length != 2)
                    continue;
                returnVal.ResponseHeaders.put(parts[0].trim(), parts[1].trim());
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
        StringBuilder headerBuilder = new StringBuilder();

        // Generate the content-length header
        int contentLength = PayloadData.length;
        ResponseHeaders.put("Content-Length", Integer.toString(contentLength));

        headerBuilder.append(String.format("%s %s %s\r\n", ProtocolVersion, ResponseCode, ResponseMessage));
        for (String headerKey : ResponseHeaders.keySet())
        {
            headerBuilder.append(String.format("%s: %s\r\n", headerKey, ResponseHeaders.get(headerKey)));
        }
        headerBuilder.append("\r\n");
        try
        {
            byte[] binary = headerBuilder.toString().getBytes(Charset.forName("UTF-8"));
            stream.write(binary);
            // Send the payload as well
            if (PayloadData.length > 0)
            {
                stream.write(PayloadData);
            }
        }
        catch (IOException e)
        {
            System.err.println(e.getMessage());
            return false;
        }
        finally
        {
            try
            {
                stream.close();
            }
            catch (IOException e2) {}
        }
        return true;
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
        ResponseHeaders.put("Content-Type", "application/x-www-form-urlencoded");
    }
    
    public static DurandalHttpResponse OKResponse()
    {
        DurandalHttpResponse returnVal = new DurandalHttpResponse();
        returnVal.ResponseCode = 200;
        returnVal.ResponseMessage = "OK";
        returnVal.ProtocolVersion = "HTTP/1.0";
        returnVal.ResponseHeaders.put("Connection", "close");
        return returnVal;
    }

    public static DurandalHttpResponse NotFoundResponse()
    {
        DurandalHttpResponse returnVal = new DurandalHttpResponse();
        returnVal.ResponseCode = 404;
        returnVal.ResponseMessage = "Not Found";
        returnVal.ProtocolVersion = "HTTP/1.0";
        returnVal.ResponseHeaders.put("Connection", "close");
        return returnVal;
    }

    public static DurandalHttpResponse ServerErrorResponse()
    {
        DurandalHttpResponse returnVal = new DurandalHttpResponse();
        returnVal.ResponseCode = 500;
        returnVal.ResponseMessage = "A server error occurred";
        returnVal.ProtocolVersion = "HTTP/1.0";
        returnVal.ResponseHeaders.put("Connection", "close");
        return returnVal;
    }

    public static DurandalHttpResponse RedirectResponse()
    {
        DurandalHttpResponse returnVal = new DurandalHttpResponse();
        returnVal.ResponseCode = 303;
        returnVal.ResponseMessage = "See Other";
        returnVal.ProtocolVersion = "HTTP/1.1";
        returnVal.ResponseHeaders.put("Connection", "close");
        return returnVal;
    }
}
