using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using MusicSyncApp.Player;

namespace MusicSyncApp.Sync
{
    public class SyncEngine
    {
        private readonly YouTubePlayer _player;
        private long _serverStartTime;
        private long _serverTimeOffset;
        private bool _isPlaying;
        private readonly DispatcherTimer _syncTimer;

        public SyncEngine(YouTubePlayer player)
        {
            _player = player;
            _syncTimer = new DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromSeconds(5);
            _syncTimer.Tick += OnSyncTick;
        }

        public void UpdateSyncInfo(long serverTime, long songStartTime, bool isPlaying)
        {
            _serverTimeOffset = serverTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _serverStartTime = songStartTime;
            _isPlaying = isPlaying;

            if (_isPlaying)
                _syncTimer.Start();
            else
                _syncTimer.Stop();
        }

        private async void OnSyncTick(object? sender, EventArgs e)
        {
            if (!_isPlaying) return;

            long currentServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _serverTimeOffset;
            double expectedPosition = (currentServerTime - _serverStartTime) / 1000.0;
            double playerPosition = await _player.GetCurrentTime();

            double drift = Math.Abs(expectedPosition - playerPosition);

            if (drift >= 0.5) // 500ms
            {
                await _player.Seek(expectedPosition);
            }
        }
    }
}
