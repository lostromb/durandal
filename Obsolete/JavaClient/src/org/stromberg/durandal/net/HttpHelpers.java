/*
 * To change this template, choose Tools | Templates
 * and open the template in the editor.
 */
package org.stromberg.durandal.net;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.net.URLDecoder;
import java.nio.charset.Charset;
import java.util.HashMap;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 *
 * @author lostromb
 */
public class HttpHelpers
{
    /// <summary>
    /// Reads a set of headers and an optional binary payload from a socket
    /// stream, and returns them as a pair.
    /// </summary>
    /// <param name="socket"></param>
    /// <returns></returns>
    public static HttpData readHttpData(InputStream stream) throws IOException
    {
        ByteArrayOutputStream headerBucket = new ByteArrayOutputStream();
        ByteArrayOutputStream payloadBucket = new ByteArrayOutputStream();

        String headerString = "";
        boolean readingPayload = false;
        int contentRemaining = Integer.MAX_VALUE;
        
        byte[] inputBuf = new byte[1024];
        int totalBytesRead = 0;
        do
        {
            int bytesRead = 0;
            bytesRead = stream.read(inputBuf);
            if (bytesRead == 0)
                continue;
            if (bytesRead < 0) // End of stream
                break;

            if (readingPayload)
            {
                payloadBucket.write(inputBuf, 0, bytesRead);
                contentRemaining -= bytesRead;
            }
            else
            {
                headerBucket.write(inputBuf, 0, bytesRead);
                int headerLength = findHttpDelimiter(headerBucket.toByteArray());

                // The client has done sending headers. See if there's any payload to read
                if (headerLength >= 0)
                {
                    // Split the incoming data between the two buckets
                    int bytesOfHeaderInThisChunk = headerLength - totalBytesRead;
                    int bytesOfPayloadAlreadyRead = bytesRead - bytesOfHeaderInThisChunk;
                    if (bytesOfPayloadAlreadyRead > 0)
                    {
                        payloadBucket.write(inputBuf, bytesOfHeaderInThisChunk, bytesOfPayloadAlreadyRead);
                    }

                    // Parse the content-length header, if it exists
                    byte[] actualHeaderBytes = new byte[totalBytesRead + bytesOfHeaderInThisChunk];
                    System.arraycopy(headerBucket.toByteArray(), 0, actualHeaderBytes, 0, actualHeaderBytes.length);
                    
                    headerString = new String(actualHeaderBytes, Charset.forName("UTF-8"));

                    Matcher contentLengthRipper = Pattern.compile("[Cc]ontent-?[Ll]ength: ?([0-9]+)").matcher(headerString);
                    if (contentLengthRipper.find())
                    {
                        contentRemaining = Integer.parseInt(contentLengthRipper.group(1)) - bytesOfPayloadAlreadyRead;
                    }
                    else if (headerString.contains("keep-alive"))
                    {
                        // If connection == keepalive, and no content length was found, assume content-length is 0
                        contentRemaining = 0;
                    }

                    readingPayload = true;
                }
            }

            totalBytesRead += bytesRead;
        } while (contentRemaining > 0);

        return new HttpData(headerString, payloadBucket.toByteArray());
    }

    // Find the \r\n\r\n delimiter between the headers and the payload, or -1 if it doesn't exist
    // The index that is returned is the index of the start of the payload
    private static int findHttpDelimiter(byte[] data)
    {
        int delimiterLocation = -1;
        for (int c = 3; c < data.length && delimiterLocation < 0; c++)
        {
            if (data[c - 3] == 13 &&
                data[c - 2] == 10 &&
                data[c - 1] == 13 &&
                data[c - 0] == 10)
                delimiterLocation = c + 1;
        }
        return delimiterLocation;
    }
    
    public static Map<String, String> getFormDataFromPayload(Map<String, String> requestHeaders, byte[] payloadData)
    {
        if (!(requestHeaders.containsKey("Content-Type") &&
            requestHeaders.get("Content-Type").contains("application/x-www-form-urlencoded")) &&
            !(requestHeaders.containsKey("Content-type") &&
            requestHeaders.get("Content-type").contains("application/x-www-form-urlencoded")) &&
            !(requestHeaders.containsKey("content-type") &&
            requestHeaders.get("content-type").contains("application/x-www-form-urlencoded")))
        {
            return null;
        }

        Map<String, String> returnVal = new HashMap<String, String>();
        String bigString = new String(payloadData, Charset.forName("UTF-8"));
        String[] parts = bigString.split("&");
        for (String part : parts)
        {
            String[] keyValue = part.split("=");
            if (keyValue.length == 2)
            {
                try
                {
                    String key = URLDecoder.decode(keyValue[0], "UTF-8");
                    String val = URLDecoder.decode(keyValue[1], "UTF-8");
                    returnVal.put(key, val);
                }
                catch (UnsupportedEncodingException e)
                {
                }
            }
        }

        return returnVal;
    }
}
