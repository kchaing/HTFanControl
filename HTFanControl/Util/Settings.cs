﻿using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HTFanControl.Util
{
    class Settings
    {
        public string MediaPlayerType { get; set; }
        public string MediaPlayerIP { get; set; }
        public int MediaPlayerPort { get; set; }
        public string ControllerType { get; set; }
        public string LIRC_IP { get; set; }
        public int LIRC_Port { get; set; }
        public string LIRC_Remote { get; set; }
        public int LIRC_ON_Delay { get; set; }
        public string MQTT_IP { get; set; }
        public int MQTT_Port { get; set; }
        public string MQTT_User { get; set; }
        public string MQTT_Pass { get; set; }
        public string MQTT_OFF_Topic { get; set; }
        public string MQTT_OFF_Payload { get; set; }
        public string MQTT_ECO_Topic { get; set; }
        public string MQTT_ECO_Payload { get; set; }
        public string MQTT_LOW_Topic { get; set; }
        public string MQTT_LOW_Payload { get; set; }
        public string MQTT_MED_Topic { get; set; }
        public string MQTT_MED_Payload { get; set; }
        public string MQTT_HIGH_Topic { get; set; }
        public string MQTT_HIGH_Payload { get; set; }
        public string MQTT_ON_Topic { get; set; }
        public string MQTT_ON_Payload { get; set; }
        public int MQTT_ON_Delay { get; set; }
        public string AudioDevice { get; set; }
        public string PlexToken { get; set; }
        public string PlexClientName { get; set; }
        public string PlexClientIP { get; set; }
        public string PlexClientPort { get; set; }
        public string PlexClientGUID { get; set; }
        public int GlobalOffsetMS { get; set; }
        public int ECOSpinupOffsetMS { get; set; }
        public int LOWSpinupOffsetMS { get; set; }
        public int MEDSpinupOffsetMS { get; set; }
        public int HIGHSpinupOffsetMS { get; set; }
        public int SpindownOffsetMS { get; set; }
        public int SprayGlobalOffsetMS { get; set; }
        public int SprayWakeToSleepDurationMS { get; set; }
        public int SpraySleepOffsetMS { get; set; }
        public int SprayActiveOffsetMS { get; set; }
        public string HTTP_Get_IP { get; set; }
        public string HTTP_Get_SPRAYON { get; set; }
        public string HTTP_Get_SPRAYOFF { get; set; }
        public string HTTP_Get_SPRAYBURST { get; set; }
        public string HTTP_Get_SPRAYSHORTBURST { get; set; }
        public string HTTP_Get_SPRAYMEDBURST { get; set; }
        public string HTTP_Get_SPRAYLONGBURST { get; set; }
        public static Settings LoadSettings()
        {
            Settings settings = new Settings();
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions();
                options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                options.WriteIndented = true;

                string jsonSettings = File.ReadAllText(Path.Combine(ConfigHelper._rootPath, "HTFanControlSettings.json"));
                settings = JsonSerializer.Deserialize<Settings>(jsonSettings, options);
            }
            catch
            {
                //default values
                settings.MediaPlayerType = "Kodi";
                settings.MediaPlayerIP = "192.168.1.100";
                settings.MediaPlayerPort = 8080;
                settings.ControllerType = "MQTT";
                settings.MQTT_IP = "127.0.0.1";
                settings.MQTT_OFF_Topic = "cmnd/HTFan/EVENT";
                settings.MQTT_OFF_Payload = "s0";
                settings.MQTT_ECO_Topic = "cmnd/HTFan/EVENT";
                settings.MQTT_ECO_Payload = "s1";
                settings.MQTT_LOW_Topic = "cmnd/HTFan/EVENT";
                settings.MQTT_LOW_Payload = "s2";
                settings.MQTT_MED_Topic = "cmnd/HTFan/EVENT";
                settings.MQTT_MED_Payload = "s3";
                settings.MQTT_HIGH_Topic = "cmnd/HTFan/EVENT";
                settings.MQTT_HIGH_Payload = "s4";
                settings.GlobalOffsetMS = 2000;
                settings.ECOSpinupOffsetMS = 1400;
                settings.LOWSpinupOffsetMS = 1200;
                settings.MEDSpinupOffsetMS = 1000;
                settings.HIGHSpinupOffsetMS = 800;
                settings.SpindownOffsetMS = 250;
                settings.SprayGlobalOffsetMS = 100;
                settings.SprayWakeToSleepDurationMS = 120000;
                settings.SpraySleepOffsetMS = 1350;
                settings.SprayActiveOffsetMS = 60;
                settings.HTTP_Get_IP = "127.0.0.1";
                settings.HTTP_Get_SPRAYON = "/SPRAY=ON";
                settings.HTTP_Get_SPRAYOFF = "/SPRAY=OFF";
                settings.HTTP_Get_SPRAYBURST = "/SPRAYDURATION=100";
                settings.HTTP_Get_SPRAYSHORTBURST = "/SPRAYDURATION=30";
                settings.HTTP_Get_SPRAYMEDBURST = "/SPRAYDURATION=300";
                settings.HTTP_Get_SPRAYLONGBURST = "/SPRAYDURATION=500";
            }

            return settings;
        }

        public static string SaveSettings(Settings settings)
        {
            string error = null;
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions();
                options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                options.WriteIndented = true;

                string jsonSettings = JsonSerializer.Serialize(settings, options);

                File.WriteAllText(Path.Combine(ConfigHelper._rootPath, "HTFanControlSettings.json"), jsonSettings);
            }
            catch(Exception e)
            {
                error = e.Message;
            }

            return error;
        }
    }
}