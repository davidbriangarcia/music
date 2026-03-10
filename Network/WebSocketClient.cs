using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MusicSyncApp.Network
{
    public class WebSocketClient
    {
        private ClientWebSocket _client;
        private CancellationTokenSource _cts;
        private readonly Uri _serverUri;

        public event Action<string> OnMessageReceived;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public WebSocketClient(string url)
        {
            _serverUri = new Uri(url);
            _client = new ClientWebSocket();
        }

        public async Task Connect()
        {
            _cts = new CancellationTokenSource();
            try
            {
                await _client.ConnectAsync(_serverUri, _cts.Token);
                OnConnected?.Invoke();
                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
                OnDisconnected?.Invoke();
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];
            var messageBuilder = new StringBuilder();

            try
            {
                while (_client.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
                            OnDisconnected?.Invoke();
                            return;
                        }

                        var part = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        messageBuilder.Append(part);
                    } while (!result.EndOfMessage);

                    var fullMessage = messageBuilder.ToString();
                    messageBuilder.Clear();
                    OnMessageReceived?.Invoke(fullMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Receive error: {ex.Message}");
                OnDisconnected?.Invoke();
            }
        }

        public async Task SendMessage(object message)
        {
            if (_client.State != WebSocketState.Open) return;

            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        public async Task Disconnect()
        {
            _cts?.Cancel();
            if (_client.State == WebSocketState.Open)
            {
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }
            OnDisconnected?.Invoke();
        }
    }
}
