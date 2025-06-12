/*
 * To change this template, choose Tools | Templates
 * and open the template in the editor.
 */
package org.stromberg.durandal.net;

import java.io.IOException;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.SocketException;
import javax.net.ServerSocketFactory;

/**
 *
 * @author lostromb
 */
public abstract class HttpServer
{
    protected int _portNum;
    private ServerThread _listenThread;
    private boolean _asyncronous;

    public HttpServer(int port, boolean asynchronous)
    {
        _portNum = port;
        _asyncronous = asynchronous;
    }

    public void startServer(String serverName)
    {
        _listenThread = new ServerThread();
        _listenThread.setName(serverName);
        _listenThread.start();
    }

    public void dispose()
    {
        stopServer();
    }

    public void stopServer()
    {
        _listenThread.cancelled = true;
        _listenThread.stop();
    }

    public int getPortNum()
    {
        return _portNum;
    }

    private class ServerThread extends Thread
    {
        public volatile boolean cancelled = false;
        
        public void run()
        {
            try
            {
                ServerSocket serverSocket = ServerSocketFactory.getDefault().createServerSocket(_portNum);

                while (!cancelled)
                {
                    Socket newSocket = serverSocket.accept();
                    if (_asyncronous)
                    {
                        new WorkerThread(newSocket).start();
                    }
                    else
                    {
                        handleConnection(newSocket);
                    }
                }
                serverSocket.close();
            }
            catch (IOException e)
            {
                System.err.println(e.getMessage());
            }
        }
    }

    private class WorkerThread extends Thread
    {
        private Socket clientSocket;
        
        public WorkerThread(Socket acceptSocket)
        {
            clientSocket = acceptSocket;
        }
        
        @Override
        public void run()
        {
            if (clientSocket != null)
            {
                handleConnection(clientSocket);
            }
        }
    }

    private void handleConnection(Socket clientSocket)
    {
        // Parse the HTTP request
        DurandalHttpRequest clientRequest = null;
        try
        {
            clientRequest = DurandalHttpRequest.readRequestFromStream(clientSocket.getInputStream());
        }
        catch (SocketException e)
        {
            System.err.println("Encountered a problem while accepting a new HTTP connection");
            System.err.println(e.getMessage());
        }
        catch (IOException e)
        {
            System.err.println("Encountered a problem while accepting a new HTTP connection");
            System.err.println(e.getMessage());
        }
        
        try
        {
            if (clientRequest != null)
            {
                DurandalHttpResponse response = handleConnection(clientRequest);

                // Validate the response
                if (response == null)
                {
                    response = DurandalHttpResponse.ServerErrorResponse();
                }
                response.writeToStream(clientSocket.getOutputStream());
            }

            if (clientSocket.isConnected())
            {
                clientSocket.close();
            }
        }
        catch (SocketException e)
        {
        }
        catch (IOException e)
        {
        }
    }

    protected abstract DurandalHttpResponse handleConnection(DurandalHttpRequest request);
}
