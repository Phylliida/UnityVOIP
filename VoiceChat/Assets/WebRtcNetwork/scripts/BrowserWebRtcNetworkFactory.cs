using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Byn.Net
{
    public class BrowserWebRtcNetworkFactory : IWebRtcNetworkFactory
    {
        private bool disposedValue = false;


        public IBasicNetwork CreateDefault(string websocketUrl, IceServer[] iceServers)
        {

            if (iceServers == null || iceServers.Length == 0 || iceServers[0] == null //one server must be set
                || iceServers[0].Urls == null || iceServers[0].Urls.Count == 0 ||
                string.IsNullOrEmpty(iceServers[0].Urls[0])) //make sure there is at least 1 valid url
            {
                Debug.LogError("Ice server missing or server url missing.");
                return null;
            }
            if (string.IsNullOrEmpty(websocketUrl))
            {
                Debug.LogError("websocketUrl missing.");
                return null;
            }
            if (iceServers.Length > 0 || string.IsNullOrEmpty(iceServers[0].Username) == false)
            {
                Debug.LogWarning("Current web implementation doesn't support username, password yet and only supports one ice server");
            }

            string[] urls = iceServers[0].Urls.ToArray();

            string iceUrlsJson = "\"" + urls[0] + "\"";
            for (int i = 1; i < urls.Length; i++)
            {
                iceUrlsJson += ", \"" + urls[i] + "\"";
            }


            //string websocketUrl = "ws://localhost:12776";
            string conf;
            if (websocketUrl == null)
            {
                conf = "{ \"signaling\" :  { \"class\": \"LocalNetwork\", \"param\" : null}, \"iceServers\":[" + iceUrlsJson + "]}";

            }
            else
            {
                conf = "{ \"signaling\" :  { \"class\": \"WebsocketNetwork\", \"param\" : \"" + websocketUrl + "\"}, \"iceServers\":[" + iceUrlsJson + "]}";
            }


            return new BrowserWebRtcNetwork(conf);
        }
        

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //free managed
                }
                //free unmanaged

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }

    }
}
