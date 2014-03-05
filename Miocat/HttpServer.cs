using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Globalization;

namespace Miocat
{
    class HttpServer
    {
        private readonly HttpListener listener;
        public bool IsListening
        {
            get { return listener.IsListening; }
        }

        public HttpServer(int port)
        {
            if (port <= 0)
                throw new ArgumentException("port must be greater than 0.", "port");

            listener = new HttpListener();
            listener.Prefixes.Add("http://*:" + port + "/");
        }

        public void Start()
        {
            Console.WriteLine("Start Listening");
            listener.Start();
            Task.Factory.StartNew(Listen);
        }

        private void Listen()
        {
            while (IsListening)
            {
                var context = listener.GetContext();
                Console.WriteLine("Accept connection");
                Task.Factory.StartNew(HandleRequest, context);
            }
        }

        private void HandleRequest(object ctx)
        {
            var context = (HttpListenerContext)ctx;
            var request = context.Request;
            var requestUri = request.Url;

            if (requestUri.AbsolutePath == "/")
            {
                GenerateResponse(context.Response, 200, "hello.");
            }
            else if (requestUri.AbsolutePath == "/request")
            {
                ProcessRequest(context, requestUri);
            }
            else
            {
                GenerateResponse(context.Response, 404, "Unknown route.");
            }
        }

        private static void ProcessRequest(HttpListenerContext context, Uri requestUri)
        {
            var query = ParseQuery(requestUri.Query);
            var culture = GetLocale(query);
            if (query.ContainsKey("target"))
            {
                var target = query["target"];
                if (IsFileUri(target, culture))
                {
                    GenerateResponse(context.Response, 500, "file: scheme is not acceptable.");
                    return;
                }
                var client = new WebClient();
                try
                {
                    var content = client.DownloadString(target);
                    GenerateResponse(context.Response, 200, content);
                }
                catch (WebException e)
                {
                    GenerateResponse(context.Response, 500, e.Message);
                }
            }
            else
            {
                GenerateResponse(context.Response, 500, "no target specified.");
            }
        }

        private static void GenerateResponse(HttpListenerResponse response, int statusCode, string body)
        {
            response.StatusCode = statusCode;
            var buffer = System.Text.Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private static CultureInfo GetLocale(IDictionary<string, string> query)
        {
            var culture = CultureInfo.InvariantCulture;
            if (query.ContainsKey("locale"))
            {
                try
                {
                    culture = CultureInfo.GetCultureInfo(query["locale"]);
                }
                catch (CultureNotFoundException)
                {
                    // do nothing, so if the culture is not found, the invariant culture is used.
                    query.Remove("locale");
                }
            }
            return culture;
        }

        private static IDictionary<string, string> ParseQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new Dictionary<string, string>();

            // skips '?', split by '&', and split by '='.
            return query.Substring(1).Split('&')
                .Select(s => s.Split('='))
                .ToDictionary(arr => arr[0], arr => Uri.UnescapeDataString(arr[1]));
        }

        private static bool IsFileUri(string uri, CultureInfo culture)
        {
            return uri.StartsWith("FILE:", true, culture);
        }
    }
}
