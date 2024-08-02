using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static ConcurrentDictionary<Guid, WebSocket> _clients = new ConcurrentDictionary<Guid, WebSocket>();
    
    private static string[] _colors = new string[] { "Red", "Green", "Blue", "Yellow", "Orange", "Purple", "Pink", "Black" };
    private static string[] _descriptions = new string[] { "Large", "Small", "Fast", "Slow", "Furry", "Spotted", "Striped", "Cute" };
    private static string[] _names = new string[] { "Lion", "Tiger", "Elephant", "Giraffe", "Monkey", "Zebra", "Kangaroo", "Panda" };

    static async Task Main(string[] args)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();
        Console.WriteLine("Listening on http://localhost:5000/");

        while (true)
        {
            HttpListenerContext context = await listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                ProcessWebSocketRequest(context);
            }
        }
    }

    static async void ProcessWebSocketRequest(HttpListenerContext context)
    {
        WebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
        WebSocket webSocket = webSocketContext.WebSocket;

        Guid clientId = Guid.NewGuid();
        string generatedName = $"{_descriptions[new Random().Next(0, _descriptions.Length)]} {_colors[new Random().Next(0, _colors.Length)]} {_names[new Random().Next(0, _names.Length)]}";
        _clients.TryAdd(clientId, webSocket);

        try
        {
            byte[] buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"{generatedName} Received: {message}");

                    await BroadcastMessage($"{generatedName}: {message}");
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            if (webSocket != null)
                webSocket.Dispose();
        }
    }

    static async Task BroadcastMessage(string message)
    {
        byte[] broadcastBuffer = Encoding.UTF8.GetBytes(message);
        foreach (var client in _clients)
        {
            if (client.Value.State == WebSocketState.Open)
            {
                try
                {
                    await client.Value.SendAsync(new ArraySegment<byte>(broadcastBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to client: {ex.Message}");
                    // Consider removing the client if there's an error
                }
            }
        }
    }
}