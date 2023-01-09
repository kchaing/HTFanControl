﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using HTFanControl.Util;

namespace HTFanControl.Controllers
{
    class LIRCController : IController
    {
        private Socket _lircSocket;
        private Settings _settings;

        private bool _isOFF = true;
        private bool _ONcmd = false;

        public string ErrorStatus { get; private set; }

        public LIRCController(Settings settings)
        {
            _settings = settings;
            _ONcmd = _settings.LIRC_ON_Delay > 0;
        }

        public bool SendCMD(string cmd)
        {
            bool send = true;
            bool goodResult = true;

            //case when fan needs to be turned ON before a command can be sent
            try
            {
                HttpResponseMessage response = null;

                using HttpClient httpClient = new HttpClient();
                switch (cmd)
                {
                    case "SPRAYOFF":
                        response = httpClient.GetAsync(_settings.HTTP_Get_IP + "/SPRAY=OFF").Result;
                        break;
                    case "SPRAYON":
                        response = httpClient.GetAsync(_settings.HTTP_Get_IP + "/SPRAY=ON").Result;
                        break;
                    case "BURST":
                        response = httpClient.GetAsync(_settings.HTTP_Get_IP + "/SPRAYDURATION=100").Result;
                        break;
                    case "SHORTBURST":
                        response = httpClient.GetAsync(_settings.HTTP_Get_IP + "/SPRAYDURATION=30").Result;
                        break;
                    case "MEDBURST":
                        response = httpClient.GetAsync(_settings.HTTP_Get_IP + "/SPRAYDURATION=300").Result;
                        break;
                    case "LONGBURST":
                        response = httpClient.GetAsync(_settings.HTTP_Get_IP + "/SPRAYDURATION=500").Result;
                        break;
                    default:
                        break;
                }

                if (response is not null)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            }
            catch (Exception e)
            {
                ErrorStatus = $"Error sending http GET request.\n\n{e.Message}";
                return false;
            }
            

            if (_ONcmd && _isOFF)
            {
                if (cmd != "OFF")
                {
                    string lircOnCMD = $"SEND_ONCE {_settings.LIRC_Remote} ON\n";
                    SendLIRCBytes(Encoding.ASCII.GetBytes(lircOnCMD));

                    Thread.Sleep(_settings.LIRC_ON_Delay);
                }
                //fan is already OFF, but it's being asked to turn OFF, so don't send a command, because that would cause it to turn ON again if ON/OFF are the same IR command
                else
                {
                    send = false;
                }
            }

            if (send)
            {
                string lircCMD = $"SEND_ONCE {_settings.LIRC_Remote} {cmd}\n";
                goodResult = SendLIRCBytes(Encoding.ASCII.GetBytes(lircCMD));
            }

            if (cmd == "OFF")
            {
                _isOFF = true;
            }
            else
            {
                _isOFF = false;
            }

            if (!goodResult)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool SendLIRCBytes(byte[] cmd)
        {
            bool tryAgain = false;
            try
            {
                _lircSocket.Send(cmd);

                if ((_lircSocket.Poll(1000, SelectMode.SelectRead) && (_lircSocket.Available == 0)) || !_lircSocket.Connected)
                {
                    throw new Exception();
                }
            }
            catch
            {
                tryAgain = true;
            }

            if (tryAgain)
            {
                try
                {
                    Thread.Sleep(75);
                    Connect();

                    _lircSocket.Send(cmd);

                    if ((_lircSocket.Poll(1000, SelectMode.SelectRead) && (_lircSocket.Available == 0)) || !_lircSocket.Connected)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Failed sending command to LIRC at: {_settings.LIRC_IP}:{_settings.LIRC_Port}";
                    return false;
                }
            }

            return true;
        }

        public bool Connect()
        {
            Disconnect();

            try
            {
                IPAddress ipAddress = IPAddress.Parse(_settings.LIRC_IP);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, _settings.LIRC_Port);
                _lircSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                IAsyncResult result = _lircSocket.BeginConnect(remoteEP, null, null);
                result.AsyncWaitHandle.WaitOne(3000);

                if (!_lircSocket.Connected)
                {
                    throw new Exception();
                }

                _lircSocket.EndConnect(result);

                Thread.Sleep(25);
            }
            catch
            {
                ErrorStatus = $"({DateTime.Now:h:mm:ss tt}) Cannot connect to LIRC at: {_settings.LIRC_IP}:{_settings.LIRC_Port}";
                return false;
            }

            Thread.Sleep(25);

            return true;
        }

        public void Disconnect()
        {
            if (_lircSocket != null)
            {
                try
                {
                    _lircSocket.Shutdown(SocketShutdown.Both);
                    _lircSocket.Close();
                }
                catch { }
            }
        }
    }
}