package org.stromberg.durandal.client;

import java.net.MalformedURLException;
import java.net.URL;
import java.nio.charset.Charset;
import org.stromberg.durandal.api.ClientRequest;
import org.stromberg.durandal.api.ClientResponse;
import org.stromberg.durandal.api.Result;
import org.stromberg.durandal.net.DurandalHttpRequest;
import org.stromberg.durandal.net.DurandalHttpResponse;
import org.stromberg.durandal.net.HttpClient;
import org.stromberg.durandal.net.HttpServer;
import org.stromberg.durandal.utils.Cache;

/**
 *
 * @author lostromb
 */
public class PresentationWebServer extends HttpServer
{
    private DialogHttpClient dialogConnection;
    private Cache<String> pageCache;
    
    public PresentationWebServer(DialogHttpClient dialogClient,
            int serverPort)
    {
        super(serverPort, true);
        dialogConnection = dialogClient;
        pageCache = new Cache<String>(10);
    }
    
    @Override
    public DurandalHttpResponse handleConnection(DurandalHttpRequest clientRequest)
    {
        DurandalHttpResponse response = DurandalHttpResponse.NotFoundResponse();

        if (clientRequest.RequestFile.startsWith("/action"))
        {
            // BEGIN TURN 2+ - Client's browser executes a dialog action
            // The key to access the associated DialogAction is encoded in the URL
            response = DurandalHttpResponse.ServerErrorResponse();

            if (clientRequest.GetParameters.containsKey("key"))
            {
                
                /*ClientRequest request = new ClientRequest();
                request.setProtocolVersion("1.0");
                // Authenticate the request
                if (_requestAuthenticator != null)
                {
                    _requestAuthenticator.AddAuthTokenToRequest(ref request);
                }
                // Call the delegate to generate a request context for us
                request.setClientContext(contextGenerator());
                // TODO: Add client ID to action URL
                String dialogTargetUri = "/action?key=" + clientRequest.GetParameters.get("key") + "&format=bond";
                ClientResponse durandalResult = dialogConnection.makeDialogActionRequest(request, dialogTargetUri);
                if (durandalResult != null && durandalResult.getExecutionResult() == Result.Success)
                {
                    // Send an HTTP 303 to redirect the client to the new response
                    // (If we just write the response directly back to the user, it could lead to double-submission and stuff)
                    String redirectUrl = GeneratePresentationUrlFromResponse(durandalResult);
                    if (redirectUrl != null)
                    {
                        response = DurandalHttpResponse.RedirectResponse();
                        response.ResponseHeaders.put("Location", redirectUrl);
                    }
                }*/
            }
        }
        else if (clientRequest.RequestFile.equals("/dialog") && clientRequest.GetParameters.containsKey("page"))
        {
            // AFTER TURN 1+ - Client's web browser talks to local cache server
            // Execute the HTTP server workflow
            String pageKey = clientRequest.GetParameters.get("page");
            response = DurandalHttpResponse.OKResponse();
            String webpage = pageCache.Retrieve(pageKey);
            // If page is null, it has expired from the cache or never existed
            /*if (webpage == null)
            {
                webpage = generateInfoPage("The requested page has expired from the server");
            }*/
            response.PayloadData = webpage.getBytes(Charset.forName("UTF-8"));
            
        }
        else if (clientRequest.RequestFile.startsWith("/views"))
        {
            // Simply pipe the request to the (remote) dialog server
            try
            {
                HttpClient client = new HttpClient(new URL(dialogConnection.getConnectionString()));
                response = client.sendRequest(clientRequest, 10000);
            }
            catch (MalformedURLException e)
            {
                response = DurandalHttpResponse.ServerErrorResponse();
            }
        }

        return response;
    }
}
