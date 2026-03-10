using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using MusicSyncApp.Network;
using MusicSyncApp.Player;
using MusicSyncApp.Sync;
using Newtonsoft.Json.Linq;

namespace MusicSyncApp
{
    public partial class MainWindow : Window
    {
        private WebSocketClient? _wsClient;
        private YouTubePlayer? _player;
        private SyncEngine? _syncEngine;

        public MainWindow()
        {
            InitializeComponent();
            _ = InitializeApp();
        }

        private async Task InitializeApp()
        {
            await PlayerWebView.EnsureCoreWebView2Async();
            
            string playerHtmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "player.html");
            if (!File.Exists(playerHtmlPath))
            {
                playerHtmlPath = Path.GetFullPath("../../../Assets/player.html");
            }
            PlayerWebView.CoreWebView2.Navigate("file:///" + playerHtmlPath.Replace("\\", "/"));

            _player = new YouTubePlayer(PlayerWebView);
            _syncEngine = new SyncEngine(_player);

            PlayerWebView.WebMessageReceived += (s, e) => {
                var raw = e.TryGetWebMessageAsString();
                try {
                    var json = JObject.Parse(raw);
                    if (json["type"]?.ToString() == "ERROR") {
                        MessageBox.Show($"YouTube Error Code: {json["code"]}", "Player Error");
                    }
                } catch {}
            };
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            try {
                if (_wsClient != null)
                {
                    await _wsClient.Disconnect();
                    _wsClient.OnMessageReceived -= HandleMessage;
                }

                string ip = ServerIpInput.Text;
                _wsClient = new WebSocketClient($"ws://{ip}:3000");
                _wsClient.OnMessageReceived += HandleMessage;
                
                await _wsClient.Connect();
                if (_player != null) await _player.SetServerIp(ip);
                MessageBox.Show($"Connected to {ip}", "Success");
            } catch (Exception ex) {
                MessageBox.Show($"Failed to connect: {ex.Message}", "Error");
            }
        }

        private void HandleMessage(string json)
        {
            Dispatcher.Invoke(async () =>
            {
                try {
                    var message = JObject.Parse(json);
                    string? type = message["type"]?.ToString();

                    if (type == "room_joined") {
                        UpdatePlaylistUI(message["playlist"] as JArray);
                        
                        long serverTime = message["serverTime"]?.Value<long>() ?? 0;
                        long songStartTime = message["songStartTime"]?.Value<long>() ?? 0;
                        bool isPlaying = message["playState"]?.ToString() == "PLAYING";
                        _syncEngine?.UpdateSyncInfo(serverTime, songStartTime, isPlaying);
                    }
                    else if (type == "sync") {
                        long serverTime = message["serverTime"]?.Value<long>() ?? 0;
                        long songStartTime = message["songStartTime"]?.Value<long>() ?? 0;
                        bool isPlaying = message["playState"]?.ToString() == "PLAYING";
                        _syncEngine?.UpdateSyncInfo(serverTime, songStartTime, isPlaying);
                    }
                    else if (type == "play") {
                        string? videoId = message["videoId"]?.ToString();
                        long startTime = message["serverStartTime"]?.Value<long>() ?? 0;
                        if (!string.IsNullOrEmpty(videoId) && _player != null)
                        {
                            await _player.LoadVideo(videoId);
                            await _player.Play();
                        }
                        _syncEngine?.UpdateSyncInfo(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), startTime, true);
                    }
                    else if (type == "pause") {
                        if (_player != null) await _player.Pause();
                        _syncEngine?.UpdateSyncInfo(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0, false);
                    }
                    else if (type == "playlist_updated") {
                        UpdatePlaylistUI(message["playlist"] as JArray);
                    }
                } catch (Exception ex) {
                    // Silently log or show error for debugging
                    Console.WriteLine($"Message handling error: {ex.Message}");
                }
            });
        }

        public class PlaylistSong
        {
            public string? VideoId { get; set; }
            public string? Title { get; set; }
            public string? Status { get; set; }
        }

        private void UpdatePlaylistUI(JArray? playlist)
        {
            try {
                PlaylistListBox.Items.Clear();
                if (playlist != null) {
                    foreach (var song in playlist)
                    {
                        PlaylistListBox.Items.Add(new PlaylistSong {
                            Title = song["title"]?.ToString() ?? "Unknown",
                            VideoId = song["videoId"]?.ToString(),
                            Status = song["status"]?.ToString()?.ToUpper() ?? "IDLE"
                        });
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"UpdatePlaylistUI error: {ex.Message}");
            }
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_wsClient == null || PlaylistListBox.SelectedIndex == -1) return;
            await _wsClient.SendMessage(new { type = "remove_song", index = PlaylistListBox.SelectedIndex });
        }

        private async void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = PlaylistListBox.SelectedIndex;
            if (_wsClient == null || index <= 0) return;
            await _wsClient.SendMessage(new { type = "move_song", fromIndex = index, toIndex = index - 1 });
            PlaylistListBox.SelectedIndex = index - 1;
        }

        private async void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = PlaylistListBox.SelectedIndex;
            if (_wsClient == null || index == -1 || index >= PlaylistListBox.Items.Count - 1) return;
            await _wsClient.SendMessage(new { type = "move_song", fromIndex = index, toIndex = index + 1 });
            PlaylistListBox.SelectedIndex = index + 1;
        }

        private async void PlaySelected_Click(object sender, RoutedEventArgs e)
        {
            if (_wsClient == null || PlaylistListBox.SelectedItem is not PlaylistSong song) return;
            if (song.Status != "READY") { MessageBox.Show("Song is still downloading!"); return; }
            await _wsClient.SendMessage(new { type = "play", videoId = song.VideoId });
        }

        private async void JoinRoom_Click(object sender, RoutedEventArgs e)
        {
            try {
                if (_wsClient == null) { MessageBox.Show("Connect to server first!"); return; }
                await _wsClient.SendMessage(new { type = "join_room", room = RoomNameInput.Text });
            } catch (Exception ex) {
                MessageBox.Show($"Join room error: {ex.Message}");
            }
        }

        private string ExtractVideoId(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            if (input.Length == 11) return input; // Already an ID

            try {
                Uri uri = new Uri(input);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["v"] ?? input;
            } catch {
                return input;
            }
        }

        private async void AddSong_Click(object sender, RoutedEventArgs e)
        {
            try {
                if (_wsClient == null) { MessageBox.Show("Connect to server first!"); return; }
                string videoId = ExtractVideoId(VideoIdInput.Text);
                await _wsClient.SendMessage(new { 
                    type = "add_song", 
                    videoId = videoId, 
                    title = "Unknown Song", 
                    addedBy = "User" 
                });
            } catch (Exception ex) {
                MessageBox.Show($"Add song error: {ex.Message}");
            }
        }

        private async void Play_Click(object sender, RoutedEventArgs e)
        {
            try {
                if (_wsClient == null) return;
                string videoId = ExtractVideoId(VideoIdInput.Text);
                await _wsClient.SendMessage(new { type = "play", videoId = videoId });
            } catch (Exception ex) {
                MessageBox.Show($"Play error: {ex.Message}");
            }
        }

        private async void Pause_Click(object sender, RoutedEventArgs e)
        {
            try {
                if (_wsClient == null) return;
                await _wsClient.SendMessage(new { type = "pause" });
            } catch (Exception ex) {
                MessageBox.Show($"Pause error: {ex.Message}");
            }
        }
    }
}
