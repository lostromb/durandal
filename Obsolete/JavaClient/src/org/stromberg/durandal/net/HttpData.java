package org.stromberg.durandal.net;

/**
 *
 * @author lostromb
 */
public class HttpData
{
    public String Headers;
    public byte[] Payload;

    public HttpData(String headers, byte[] payload)
    {
        Headers = headers;
        Payload = payload;
    }
}
