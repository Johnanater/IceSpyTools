using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Cysharp.Threading.Tasks;
using IceSpyTools.Models;
using OpenMod.API;
using OpenMod.Unturned.Plugins;
using OpenMod.API.Plugins;
using OpenMod.Core.Helpers;
using SDG.Unturned;
using Steamworks;

[assembly: PluginMetadata("IceSpyTools", DisplayName = "Johnanater.IceSpyTools", Author = "Johnanater", Website = "https://johnanater.com")]
namespace IceSpyTools
{
    public class IceSpyTools : OpenModUnturnedPlugin
    {
        private readonly IConfiguration m_Configuration;
        private readonly IStringLocalizer m_StringLocalizer;
        private readonly ILogger<IceSpyTools> m_Logger;
        private readonly IRuntime m_Runtime;

        public Config Config;

        private bool _loaded;

        public IceSpyTools(
            IConfiguration configuration, 
            IStringLocalizer stringLocalizer,
            ILogger<IceSpyTools> logger, 
            IRuntime runtime,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_Configuration = configuration;
            m_StringLocalizer = stringLocalizer;
            m_Logger = logger;
            m_Runtime = runtime;
        }

        protected override async UniTask OnLoadAsync()
        {
            Config = m_Configuration.Get<Config>();
            
			await UniTask.SwitchToMainThread();
            
            if (Config.DeathSpy)
                PlayerLife.onPlayerDied += OnPlayerDied;
            
            if (Config.TimedSpy)
                AsyncHelper.Schedule("TimeSpyThread", TimeSpy);
            
            _loaded = true;
            m_Logger.LogInformation($"Successfully loaded {GetType()} by Johnanater, version {Version}");
        }

        protected override async UniTask OnUnloadAsync()
        {
            await UniTask.SwitchToMainThread();

            if (Config.DeathSpy)
                PlayerLife.onPlayerDied -= OnPlayerDied;

            _loaded = false;
            m_Logger.LogInformation(m_StringLocalizer[$"Successfully unloaded {GetType()} by Johnanater, version {Version}"]);
        }
        
        private void OnPlayerDied(PlayerLife victim, EDeathCause cause, ELimb limb, CSteamID instigator)
        {
            var steamKiller = PlayerTool.getSteamPlayer(instigator);

            steamKiller?.player.sendScreenshot(CSteamID.Nil, OnDeathSpyReady);
        }

        private void OnDeathSpyReady(CSteamID steamId, byte[] screenshotData)
        {
            AsyncHelper.Schedule("OnDeathSpyReady", () => WriteScreenshot(steamId, screenshotData, ScreenshotType.KillSpy));
        }

        private void OnTimeSpyReady(CSteamID steamId, byte[] screenshotData)
        {
            AsyncHelper.Schedule("OnTimeSpyReady", () => WriteScreenshot(steamId, screenshotData, ScreenshotType.TimedSpy));
        }

        private async Task TimeSpy()
        {
            while (_loaded)
            {
                await UniTask.SwitchToMainThread();

                foreach (var steamPlayer in Provider.clients)
                {
                    steamPlayer.player.sendScreenshot(CSteamID.Nil, OnTimeSpyReady);
                }
                
                await UniTask.SwitchToThreadPool();
                await Task.Delay(TimeSpan.FromSeconds(Config.TimedSpySeconds));
            }
        }

        private async Task WriteScreenshot(CSteamID steamId, byte[] screenshotData, ScreenshotType type)
        {
            var screenshotDir = Path.Combine(m_Runtime.WorkingDirectory, "plugins", "IceSpyTools", "screenshots", type.ToString());
            var fileLoc = $"{screenshotDir}/{steamId}";
            var fileExtension = ".jpg";

            CheckDir(screenshotDir);

            if (Config.KeepBackups && File.Exists(fileLoc + fileExtension))
                MoveToBackup(fileLoc + fileExtension, steamId, type);
            
            using (var stream = new FileStream(fileLoc + fileExtension, FileMode.OpenOrCreate))
            {
                await stream.WriteAsync(screenshotData, 0, screenshotData.Length);
                Debug($"Finished writing spy for {steamId} for {type}");
            }
        }

        private void MoveToBackup(string origFile, CSteamID steamId, ScreenshotType type)
        {
            var bakDir = Path.Combine(m_Runtime.WorkingDirectory, "plugins", "IceSpyTools", "screenshots", "backups", type.ToString());
            var bakFilePrefix = $"{bakDir}/{steamId}_";
            var fileExtension = ".jpg";
            
            CheckDir(bakDir);

            // loop through allowed in reverse, delete the last, up the rest
            for (var index = Config.BackupNumber; index >= 1; index--)
            {
                var fullName = $"{bakFilePrefix}{index}{fileExtension}";

                if (!File.Exists(fullName))
                    continue;
                
                if (index == Config.BackupNumber)
                    File.Delete(fullName);
                else
                    File.Move(fullName, $"{bakFilePrefix}{index + 1}{fileExtension}");
            }
            File.Move(origFile, bakFilePrefix + "1" + fileExtension);
            Debug($"Backed up {steamId} {type}.");
        }

        private void CheckDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private void Debug(string msg)
        {
            if (Config.Debug)
                m_Logger.LogInformation(msg);
        }
    }
}
