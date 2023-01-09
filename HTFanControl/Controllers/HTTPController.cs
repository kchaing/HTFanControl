using HTFanControl.Util;
using System;
using System.Net.Http;

namespace HTFanControl.Controllers
{
    class HTTPController : IController
    {
        private Settings _settings;
        public string ErrorStatus { get; private set; }

        public HTTPController(Settings settings)
        {
            _settings = settings;
        }

        public bool Connect()
        {
            return true;
        }

        public void Disconnect() { }

        public bool SendCMD(string cmd)
        {
            string httpgeturl = "";

            switch (cmd)
            {
                case "SPRAYOFF":
                    httpgeturl = _settings.HTTP_Get_IP + _settings.HTTP_Get_SPRAYOFF;
                    break;
                case "SPRAYON":
                    httpgeturl = _settings.HTTP_Get_IP + _settings.HTTP_Get_SPRAYON;
                    break;
                case "BURST":
                    httpgeturl = _settings.HTTP_Get_IP + _settings.HTTP_Get_SPRAYBURST;
                    break;
                case "SHORTBURST":
                    httpgeturl = _settings.HTTP_Get_IP + _settings.HTTP_Get_SPRAYSHORTBURST;
                    break;
                case "MEDBURST":
                    httpgeturl = _settings.HTTP_Get_IP + _settings.HTTP_Get_SPRAYMEDBURST;
                    break;
                case "LONGBURST":
                    httpgeturl = _settings.HTTP_Get_IP + _settings.HTTP_Get_SPRAYLONGBURST;
                    break;
                default:
                    break;
            }

            using HttpClient httpClient = new HttpClient();

            try
            {
                HttpResponseMessage result = httpClient.GetAsync(httpgeturl).Result;

                if (!result.IsSuccessStatusCode)
                {
                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                ErrorStatus = $"Error sending http GET request: {httpgeturl}\n\n{e.Message}";
                return false;
            }

            return true;
        }
    }
}