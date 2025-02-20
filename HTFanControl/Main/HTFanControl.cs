﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HTFanControl.Timers;
using System.IO.Compression;
using HTFanControl.Controllers;
using HTFanControl.Players;
using HTFanControl.Util;

namespace HTFanControl.Main
{
    class HTFanControl
    {
        public string _errorStatus;
        public string _windtrackError;
        public long _loadedVideoTime;
        public string _loadedVideoFilename;
        public string _currentWindtrackPath;
        public string _windTrackHeader;
        public int _curCmdIndex = -1;
        public int _nextCmdIndex;
        public bool _isPlaying = false;
        public bool _isEnabled = true;
        public bool _sprayIsEnabled = true;
        public bool _hasOffset = false;
        public double _offset;
        public bool _offsetEnabled = false;

        public List<Tuple<TimeSpan, string>> _videoTimeCodes;

        private PositionTimer _videoTimer;
        private readonly Timer _syncTimer;
        private IPlayer _mediaPlayer;
        public AudioSync _audioSync;
        public IController _fanController;

        public Settings _settings;

        public Log _log;

        public HTFanControl()
        {
            _log = new Log();

            _settings = Settings.LoadSettings();
            Settings.SaveSettings(_settings);

            _syncTimer = new Timer(SyncTimerTick, null, Timeout.Infinite, Timeout.Infinite);

            SelectSyncSource();

            SelectController();
        }

        public void ReInitialize(bool fullRefresh)
        {
            _videoTimer?.Stop();
            _loadedVideoFilename = "";
            _windTrackHeader = "";
            _loadedVideoTime = 0;
            _videoTimeCodes = null;
            _curCmdIndex = -1;
            _nextCmdIndex = 0;
            _isPlaying = false;

            if (_settings.MediaPlayerType == "Audio")
            {
                _audioSync?.Stop();
            }

            SelectSyncSource();

            if (fullRefresh)
            {
                SelectController();
            }
        }

        private void SelectController()
        {
            _fanController?.Disconnect();

            _fanController = _settings.ControllerType switch
            {
                "LIRC" => new LIRCController(_settings),
                "MQTT" => new MQTTController(_settings),
                "HTTP" => new HTTPController(_settings),
                _ => new LIRCController(_settings),
            };

            if(!_fanController.Connect())
            {
                _errorStatus = _fanController.ErrorStatus;
            }
        }

        private void SelectSyncSource()
        {
            if (_settings.MediaPlayerType == "Audio")
            {
                _audioSync = new AudioSync(this);
                _syncTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                _mediaPlayer = _settings.MediaPlayerType switch
                {
                    "MPC" => new MPCPlayer(_settings),
                    "Kodi" => new KodiPlayer(_settings),
                    "KodiMPC" => new KodiPlayer(_settings),
                    "Plex" => new PlexPlayer(_settings),
                    "RokuPlex" => new RokuPlexPlayer(_settings),
                    _ => new MPCPlayer(_settings),
                };

                _syncTimer.Change(1000, Timeout.Infinite);
            }
        }

        public async void SelectVideo(string fileName)
        {
            ExtractWindtrack(Path.Combine(ConfigHelper._rootPath, "windtracks", fileName + ".zip"), true);

            _loadedVideoFilename = "Loading Video Fingerprints...";
            _windTrackHeader = "Loading Windtrack...";

            _audioSync.Start(fileName);
            _loadedVideoFilename = fileName;
            LoadVideoTimecodes(fileName, "");

            if (_videoTimer != null)
            {
                await _videoTimer.DisposeAsync(_videoTimeCodes == null);
            }
            if (_videoTimeCodes != null)
            {
                _videoTimer = new PositionTimer<(string, int)>(_videoTimeCodes.Select((v, i) => (v.Item1, (v.Item2, i))), SendCmd, 1000, ("OFF", -1));
            }
            else
            {
                _videoTimer = null;
            }
        }

        public void UpdateTime()
        {
            _videoTimer.Update(TimeSpan.FromMilliseconds(_loadedVideoTime));
        }

        public void ToggleFan()
        {
            if (_isEnabled)
            {
                _isEnabled = false;

                if (!_fanController.SendCMD("OFF"))
                {
                    _errorStatus = _fanController.ErrorStatus;
                }
                _log.LogMsg("Fans Disabled");
            }
            else
            {
                _log.LogMsg("Fans Enabled");

                _isEnabled = true;

                if (_videoTimer != null)
                {
                    try
                    {
                        if (_isPlaying)
                        {
                            if (!_fanController.SendCMD(_videoTimeCodes[_curCmdIndex].Item2))
                            {
                                _errorStatus = _fanController.ErrorStatus;
                            }
                            _log.LogMsg($"Sent CMD: {_videoTimeCodes[_curCmdIndex].Item1.ToString("G").Substring(2, 12)},{_videoTimeCodes[_curCmdIndex].Item2}");
                        }
                    }
                    catch { }
                }
            }
        }

        public void ToggleSpray()
        {
            if (_sprayIsEnabled)
            {
                _sprayIsEnabled = false;

                if (!_fanController.SendCMD("SPRAYOFF"))
                {
                    _errorStatus = _fanController.ErrorStatus;
                }
                _log.LogMsg("Spray Disabled");
            }
            else
            {
                _log.LogMsg("Spray Enabled");

                _sprayIsEnabled = true;

                if (_videoTimer != null)
                {
                    try
                    {
                        if (_isPlaying) //I think this logic will send prev command after playing starts which we dont want
                        {                            
                            if (!_fanController.SendCMD(_videoTimeCodes[_curCmdIndex].Item2))
                            {
                                _errorStatus = _fanController.ErrorStatus;
                            }
                            _log.LogMsg($"Sent CMD: {_videoTimeCodes[_curCmdIndex].Item1.ToString("G").Substring(2, 12)},{_videoTimeCodes[_curCmdIndex].Item2}");                           
                        }
                    }
                    catch { }
                }
            }
        }

        private void SyncTimerTick(object o)
        {
            SyncVideo();
        }

        private async void SyncVideo()
        {
            if (_mediaPlayer != null)
            {
                bool success = _mediaPlayer.Update();

                if (success)
                {
                    _isPlaying = _mediaPlayer.IsPlaying;
                    _loadedVideoTime = _mediaPlayer.VideoTime;

                    if (_loadedVideoFilename != _mediaPlayer.FileName)
                    {
                        _loadedVideoFilename = _mediaPlayer.FileName;
                        LoadVideoTimecodes(_mediaPlayer.FileName, _mediaPlayer.FilePath);

                        if (_videoTimer != null)
                        {
                            await _videoTimer.DisposeAsync(_videoTimeCodes == null);
                        }
                        if (_videoTimeCodes != null)
                        {
                            _videoTimer = new PositionTimer<(string, int)>(_videoTimeCodes.Select((v, i) => (v.Item1, (v.Item2, i))), SendCmd, _mediaPlayer.VideoTimeResolution, ("OFF", -1));
                        }
                        else
                        {
                            _videoTimer = null;
                        }
                    }

                    if (_videoTimer != null)
                    {
                        if (_mediaPlayer.IsPlaying)
                        {
                            _videoTimer.Update(TimeSpan.FromMilliseconds(_mediaPlayer.VideoTime));
                        }
                        else
                        {
                            _videoTimer.Stop();
                        }
                    }
                }
                else
                {
                    _errorStatus = _mediaPlayer.ErrorStatus;
                    ReInitialize(false);
                }
            }

            if (_settings.MediaPlayerType != "Audio")
            {
                _syncTimer.Change(1000, Timeout.Infinite);
            }
        }

        private void SendCmd(PositionTimer videoTimer, (string cmd, int index) command)
        {
            _log.LogMsg($"Entered SendCmd function");

            (string fanCmd, int i) = command;
            _curCmdIndex = i;

            if(i == -1)
            {
                fanCmd = "OFF";
            }
            else
            {
                _nextCmdIndex = i + 1;
            }

            try
            {
                _log.LogMsg($"Sent CMD: {_videoTimeCodes[i].Item1.ToString("G").Substring(2, 12)},{fanCmd}");
            }
            catch
            {
                _log.LogMsg($"Sent CMD: {fanCmd}");
            }

            bool isFanCmd = false;
            bool isSprayCmd = false;
            
            if (fanCmd.Equals("OFF") || fanCmd.Equals("ECO") || fanCmd.Equals("LOW") || fanCmd.Equals("MED") || fanCmd.Equals("HIGH"))
            {
                isFanCmd = true;
            }
            if (fanCmd.Equals("SPRAYOFF") || fanCmd.Equals("SPRAYON") || fanCmd.Equals("BURST") || fanCmd.Equals("SHORTBURST") || fanCmd.Equals("MEDBURST") || fanCmd.Equals("LONGBURST"))
            {
                isSprayCmd = true;
            }
            

            if (_isEnabled && isFanCmd)
            {
                if (!_fanController.SendCMD(fanCmd))
                {
                    _errorStatus = _fanController.ErrorStatus;
                }
            }

            if (_sprayIsEnabled && isSprayCmd) //add spray offset logic here?
            {
                _log.LogMsg($"Entered Spray Is Enabled statement: {_sprayIsEnabled}");
                if (!_fanController.SendCMD(fanCmd))
                {
                    _errorStatus = _fanController.ErrorStatus;
                }
            }

            Thread.Sleep(250);
        }

        private string GetWindtrackFilePath(string fileName, string filePath)
        {
            string validFilePath = null;

            //LEGACY look in windtrack folder for .txt
            try
            {
                if (string.IsNullOrEmpty(validFilePath) && File.Exists(Path.Combine(ConfigHelper._rootPath, "windtracks", fileName + ".txt")))
                {
                    validFilePath = Path.Combine(ConfigHelper._rootPath, "windtracks", fileName + ".txt");
                }
            }
            catch { }

            //LEGACY check the active video's folder for .txt
            try
            {
                if (string.IsNullOrEmpty(validFilePath) && File.Exists(Path.Combine(filePath, fileName + ".txt")))
                {
                    validFilePath = Path.Combine(filePath, fileName + ".txt");
                }
            }
            catch { }

            //look for windtrack .zip archive in windtracks folder
            try
            {
                if (string.IsNullOrEmpty(validFilePath) && File.Exists(Path.Combine(ConfigHelper._rootPath, "windtracks", fileName + ".zip")))
                {
                    ExtractWindtrack(Path.Combine(ConfigHelper._rootPath, "windtracks", fileName + ".zip"), false);
                    validFilePath = Path.Combine(ConfigHelper._rootPath, "tmp", fileName + ".txt");
                }
            }
            catch { }

            //if not found, look in the active video's folder
            try
            {
                if (string.IsNullOrEmpty(validFilePath) && File.Exists(Path.Combine(filePath, fileName + ".zip")))
                {
                    ExtractWindtrack(Path.Combine(filePath, fileName + ".zip"), false);
                    validFilePath = Path.Combine(ConfigHelper._rootPath, "tmp", fileName + ".txt");
                }
            }
            catch { }
 
            return validFilePath;
        }

        private void LoadVideoTimecodes(string fileName, string filePath)
        {
            string validFilePath = GetWindtrackFilePath(fileName, filePath);

            if (!string.IsNullOrEmpty(validFilePath))
            {
                if (validFilePath != _currentWindtrackPath)
                {
                    _currentWindtrackPath = validFilePath;
                    _offsetEnabled = false;
                }

                _windtrackError = null;

                _videoTimeCodes = new List<Tuple<TimeSpan, string>>();
                _windTrackHeader = "";

                string[] lines = File.ReadAllLines(validFilePath);
                _offset = 0;
                _hasOffset = false;
                string lastCmd = "OFF";
                double rawPrevTime = -500;
                double actualPrevTime = -500;
                double lastSprayCmd = -500;

                bool verifyOrder = true;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    //header lines
                    if (line.StartsWith("#") && _videoTimeCodes.Count == 0)
                    {
                        _windTrackHeader += line.TrimStart(new[] { '#', ' ' }) + "<br \\>";

                        //offset line
                        if (line.ToLower().Contains("offset:"))
                        {
                            try
                            {
                                TimeSpan ts = TimeSpan.Parse(line.Substring(line.IndexOf('(') + 1, line.LastIndexOf(')') - line.IndexOf('(') - 1));
                                _offset = ts.TotalMilliseconds;
                                _hasOffset = true;
                            }
                            catch { }
                        }

                        if (line.ToLower().Contains("enableoffset"))
                        {
                            _offsetEnabled = true;
                        }
                    }
                    //non-comment or blank lines
                    else if (!line.StartsWith(@"\\") && !line.StartsWith(@"//") && line.Length > 0)
                    {
                        //parse line
                        string[] lineData = line.Split(',');
                        string lineTime = lineData[0];
                        string lineCmd = lineData[1];

                        // check if this timecode contains a fan command or spray (global offset only) so we can apply offsets
                        bool isFanCmd = false;
                        bool isSprayCmd = false;
                        for (int j = 1; j < lineData.Length; j++)
                        {
                            if (lineData[j].Equals("OFF") || lineData[j].Equals("ECO") || lineData[j].Equals("LOW") || lineData[j].Equals("MED") || lineData[j].Equals("HIGH"))
                            {
                                isFanCmd = true;
                            }
                            if (lineData[j].Equals("SPRAYOFF") || lineData[j].Equals("SPRAYON") || lineData[j].Equals("BURST") || lineData[j].Equals("SHORTBURST") || lineData[j].Equals("MEDBURST") || lineData[j].Equals("LONGBURST"))
                            {
                                isSprayCmd = true;
                            }
                        }

                        double? timeCode = null;
                        try
                        {
                            timeCode = TimeSpan.Parse(lineTime).TotalMilliseconds;
                        }
                        catch
                        {
                            if (_windtrackError is null)
                            {
                                _windtrackError = $"Bad timecode on line {i+1}: {line}";
                            }
                        }

                        if(timeCode != null)
                        {
                            if (isFanCmd)
                            {
                                timeCode = timeCode - _settings.GlobalOffsetMS;
                            }

                            if (isSprayCmd) //add global spray offset and sleep/active offsets
                            {
                                _log.LogMsg($"SprayCMD: {lineData[1]}, Last Spray Cmd: {lastSprayCmd}, Raw Timecode: {timeCode}");
                                timeCode = timeCode - _settings.SprayGlobalOffsetMS;
                                _log.LogMsg($"Applying Spray Global Offset: {timeCode}");
                                if ((lastSprayCmd < 0 || timeCode - lastSprayCmd > _settings.SprayWakeToSleepDurationMS)) 
                                {
                                    timeCode = timeCode - _settings.SpraySleepOffsetMS;                        
                                    _log.LogMsg($"Applying Spray Sleep Offset: {timeCode}");
                                }
                                else
                                {
                                    timeCode = timeCode - _settings.SprayActiveOffsetMS;
                                    _log.LogMsg($"Applying Spray Active Offset: {timeCode}");
                                }
                                                                                            
                                //if offset makes timecode invalid, fix it
                                if (timeCode < lastSprayCmd + 60)
                                {                                    
                                    timeCode = lastSprayCmd + 60;
                                    _log.LogMsg($"Invalid spray command. Adding 60 ms: {timeCode}");
                                }
                                lastSprayCmd = (double)timeCode;
                            }

                            rawPrevTime = (double)timeCode;

                            if (isFanCmd)
                            {
                                //if command comes after OFF, add spinup offset
                                if (lastCmd == "OFF")
                                {
                                    if(lineCmd == "ECO")
                                    {
                                        timeCode -= _settings.ECOSpinupOffsetMS;
                                    }
                                    else if (lineCmd == "LOW")
                                    {
                                        timeCode -= _settings.LOWSpinupOffsetMS;
                                    }
                                    else if (lineCmd == "MED")
                                    {
                                        timeCode -= _settings.MEDSpinupOffsetMS;
                                    }
                                    else if (lineCmd == "HIGH")
                                    {
                                        timeCode -= _settings.HIGHSpinupOffsetMS;
                                    }
                                }
                                //if command is OFF, add spindown offset
                                else if (lineCmd == "OFF")
                                {
                                    timeCode -= _settings.SpindownOffsetMS;
                                }
                                //if offset makes timecode invalid, fix it
                                if (timeCode < actualPrevTime + 500)
                                {
                                    timeCode = actualPrevTime + 500;
                                    _log.LogMsg($"Invalid fan command. Adding 500 ms: {timeCode}");
                                }
                            }

                            //ignore offset if it's not enabled
                            if(!_offsetEnabled)
                            {
                                _offset = 0;
                            }

                            //keep clearing the list if the timecode is less than or equal to 0 so that we only end up with 1 timecode at 0 at the start
                            if ((timeCode + _offset) <= 0)
                            {
                                _videoTimeCodes.Clear();
                                timeCode = 0;
                            }

                            _videoTimeCodes.Add(new Tuple<TimeSpan, string>(TimeSpan.FromMilliseconds((double)timeCode + _offset), lineData[1]));

                            if (isFanCmd)
                            {
                                lastCmd = lineCmd;
                                actualPrevTime = (double)timeCode;
                            }
                        }
                    }
                }

                //sort timecodes just to be safe for special cases where they could be out of order
                _videoTimeCodes = _videoTimeCodes.OrderBy(v => v.Item1).ToList();
            }
            else
            {
                _videoTimeCodes = null;
            }
        }

        private void ExtractWindtrack(string filePath, bool extractFingerprint)
        {
            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(ConfigHelper._rootPath, "tmp"));

                foreach (FileInfo file in directoryInfo.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
                {
                    dir.Delete(true);
                }
            }
            catch { }

            string fileName = Path.GetFileNameWithoutExtension(filePath);

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(filePath);

                archive.Entries.Where(e => e.Name.Equals("commands.txt")).Single().ExtractToFile(Path.Combine(ConfigHelper._rootPath, "tmp", fileName + ".txt"), true);

                if (extractFingerprint)
                {
                    Directory.CreateDirectory(Path.Combine(ConfigHelper._rootPath, "tmp", "fingerprint"));
                    archive.Entries.Where(e => e.Name.Equals("full.fingerprints")).Single().ExtractToFile(Path.Combine(new string[] { ConfigHelper._rootPath, "tmp", "fingerprint", "audio" }), true);
                }
            }
            catch
            {
                _errorStatus = $"Failed to extract windtrack file: {filePath}";
            }
        }
    }
}