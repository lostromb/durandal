package org.stromberg.durandal.net;

import java.io.BufferedOutputStream;
import java.io.IOException;
import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.MalformedURLException;
import java.net.Socket;
import java.net.URL;
import java.net.URLConnection;
import javax.net.SocketFactory;

/**
 * Base class for a simple HTTP client
 * @author lostromb
 */
public class HttpClient
{
    private URL _remoteHost;

    public HttpClient(URL url)
    {
        _remoteHost = url;
    }

    public HttpClient(String remoteHost, int remotePort)
    {
        try
        {
            _remoteHost = new URL("http", remoteHost, remotePort, "/");
        }
        catch (MalformedURLException e)
        {
            
        }
    }

    /// <summary>
    /// The http://0.0.0.0:port form of the address used to connect to the server
    /// </summary>
    public String getServerAddress()
    {
        return _remoteHost.toString();
    }

    public byte[] sendRequest(byte[] payload, String targetFile, int readTimeout)
    {
        DurandalHttpRequest request = new DurandalHttpRequest();
        request.RequestMethod = "GET";
        request.RequestFile = targetFile;
        request.ProtocolVersion = "HTTP/1.0";
        request.PayloadData = payload;
        DurandalHttpResponse response = sendRequest(request, readTimeout);
        if (response != null && response.ResponseCode == 200)
        {
            return response.PayloadData;
        }

        return new byte[0];
    }

    public DurandalHttpResponse sendRequest(DurandalHttpRequest request, int readTimeout)
    {
        Socket remoteSocket = null;
        try
        {
            InetAddress remoteAddress = Inet4Address.getByName(_remoteHost.getHost());
            remoteSocket = SocketFactory.getDefault().createSocket(remoteAddress, _remoteHost.getPort());
            
            // Make the request
            BufferedOutputStream output = new BufferedOutputStream(remoteSocket.getOutputStream());
            request.writeToStream(output);
            output.flush();

            // Get the response
            DurandalHttpResponse response = DurandalHttpResponse.readResponseFromStream(remoteSocket.getInputStream());
            
            remoteSocket.close();
            
            if (response == null)
                return DurandalHttpResponse.NotFoundResponse();
            return response;
        }
        catch (MalformedURLException e)
        {
            return null;
        }
        catch (IOException e)
        {
            return null;
        }
        finally
        {
            if (remoteSocket != null && remoteSocket.isConnected())
            {
                try
                {
                    remoteSocket.close();
                }
                catch (IOException e) {}
            }
        }
    }
}
