using System;
using System.Net;
using System.Text;
using System.Threading;

namespace WController.Plugin.Prompts.Network;

public class Server
{
    private readonly HttpListener _listener;
    private Action<HttpListenerRequest, HttpListenerResponse> _callback;
    private Thread _thread;

    public Server(int port)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:" + port + "/");
    }

    public void MapRequest(Action<HttpListenerRequest, HttpListenerResponse> callback)
    {
        _callback = callback;
        Start();
    }

    private void Start()
    {
        _listener.Start();
        _thread = new Thread(ListenLoop);
        _thread.IsBackground = true;
        _thread.Start();
    }

    private void ListenLoop()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = _listener.GetContext();
                _callback?.Invoke(context.Request, context.Response);
                context.Response.OutputStream.Close();
            }
            catch { }
        }
    }

    public void Stop()
    {
        _listener.Stop();
    }
}
