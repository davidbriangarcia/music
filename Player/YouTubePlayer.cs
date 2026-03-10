using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Threading.Tasks;

namespace MusicSyncApp.Player
{
    public class YouTubePlayer
    {
        private readonly WebView2 _webView;
        public event Action OnPlayerReady;
        public event Action<int, double> OnPlayerStateChanged;

        public YouTubePlayer(WebView2 webView)
        {
            _webView = webView;
            _webView.WebMessageReceived += WebView_WebMessageReceived;
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            // In case it's a JSON string, we parse it
            // Simple logic for now:
            if (message.Contains("READY"))
            {
                OnPlayerReady?.Invoke();
            }
            else if (message.Contains("STATE_CHANGE"))
            {
                // Logic to parse state and time from JSON
                // For brevity, I'll use a more robust parsing in a real scenario
            }
        }

        public async Task SetServerIp(string ip)
        {
            await _webView.ExecuteScriptAsync($"setServerIp('{ip}')");
        }

        public async Task LoadVideo(string videoId)
        {
            await _webView.ExecuteScriptAsync($"loadVideo('{videoId}')");
        }

        public async Task Play()
        {
            await _webView.ExecuteScriptAsync("playVideo()");
        }

        public async Task Pause()
        {
            await _webView.ExecuteScriptAsync("pauseVideo()");
        }

        public async Task Seek(double seconds)
        {
            await _webView.ExecuteScriptAsync($"seekTo({seconds})");
        }

        public async Task<double> GetCurrentTime()
        {
            var result = await _webView.ExecuteScriptAsync("getCurrentTime()");
            if (double.TryParse(result, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double time))
            {
                return time;
            }
            return 0;
        }
    }
}
