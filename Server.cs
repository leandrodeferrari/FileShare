using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FileShare
{
    internal class Server
    {
        private static int activeRequests = 0;

        static void Main(string[] args)
        {
            JObject config = LoadConfiguration("appsettings.json");
            string fileServingPath = config["ServerConfig"]["FileServingPath"].ToString();
            int port = (int)config["ServerConfig"]["Port"];
            TcpListener server = new TcpListener(IPAddress.Loopback, port);

            server.Start();

            Console.WriteLine($"Servidor de FileShare iniciado en el puerto {port}. Esperando conexiones... {Environment.NewLine}");


            while (true)
            {
                TcpClient client = server.AcceptTcpClient();

                ThreadPool.QueueUserWorkItem(state => HandleClient(client, fileServingPath));
            }
        }

        static JObject LoadConfiguration(string jsonPath)
        {
            string json = File.ReadAllText(jsonPath);
            return JObject.Parse(json);
        }

        static void HandleClient(TcpClient client, string fileServingPath)
        {
            Interlocked.Increment(ref activeRequests);

            IPEndPoint remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            string clientIp = remoteEndPoint?.Address.ToString() ?? "IP desconocida";

            Console.WriteLine($"Solicitud recibida de IP: {clientIp}. Hilo: {Thread.CurrentThread.ManagedThreadId}. Solicitudes activas: {activeRequests}");

            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream);
            StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            try
            {
                string request = reader.ReadLine();
                HandleHttpRequest(request, clientIp, reader, writer, stream, fileServingPath);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error en la solicitud desde IP: {clientIp} (Hilo: {Thread.CurrentThread.ManagedThreadId}).{Environment.NewLine}Mensaje de error: {ex.Message}.");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Error de red en la solicitud desde IP: {clientIp} (Hilo: {Thread.CurrentThread.ManagedThreadId}).{Environment.NewLine}Mensaje de error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error desconocido en la solicitud desde IP: {clientIp} (Hilo: {Thread.CurrentThread.ManagedThreadId}).{Environment.NewLine}Mensaje de error: {ex.Message}");
            }
            finally
            {
                client.Close();
                Interlocked.Decrement(ref activeRequests);

                Console.WriteLine($"Solicitud completada de IP: {clientIp}. Hilo: {Thread.CurrentThread.ManagedThreadId}. Solicitudes activas: {activeRequests}");
                PrintDivider();
            }
        }

        static void HandleHttpRequest(string request, string clientIp, StreamReader reader, StreamWriter writer, NetworkStream stream, string fileServingPath)
        {
            if (string.IsNullOrEmpty(request)) return;

            string httpMethod = GetHttpMethod(request);

            if (httpMethod == "GET")
            {
                string requestedFile = GetRequestedFile(request);
                string queryParams = GetQueryParams(request);

                HandleGetRequest(writer, stream, fileServingPath, clientIp, requestedFile, queryParams);

                LogRequest(clientIp, httpMethod, requestedFile, queryParams, "");
            }
            else if (httpMethod == "POST")
            {
                string body = HandlePostRequest(reader, writer);

                LogRequest(clientIp, httpMethod, "No aplica", "", body);
            }
        }

        static string GetHttpMethod(string request)
        {
            string[] requestParts = request.Split(' ');
            return requestParts.Length > 0 ? requestParts[0] : "DESCONOCIDO";
        }

        static string GetRequestedFile(string request)
        {
            string[] parts = request.Split(' ');
            if (parts.Length > 1 && parts[1] != "/")
            {
                string[] fileAndParams = parts[1].Split('?');
                return fileAndParams[0].TrimStart('/');
            }
            return "index.html";
        }

        static string GetQueryParams(string request)
        {
            string[] parts = request.Split(' ');
            if (parts.Length > 1 && parts[1].Contains("?"))
            {
                string[] fileAndParams = parts[1].Split('?');
                return fileAndParams.Length > 1 ? fileAndParams[1] : "";
            }
            return "";
        }

        static void HandleGetRequest(StreamWriter writer, NetworkStream stream, string fileServingPath, string clientIp, string requestedFile, string queryParams)
        {
            Console.WriteLine($"Archivo solicitado: {requestedFile} (IP: {clientIp}, Hilo: {Thread.CurrentThread.ManagedThreadId})");

            string filePath = Path.Combine(fileServingPath, requestedFile);

            if (File.Exists(filePath))
            {
                ServeFile(writer, stream, filePath);
            }
            else
            {
                ServeErrorPage(writer, stream, fileServingPath, "404.html");
            }
        }

        static void ServeFile(StreamWriter writer, NetworkStream stream, string filePath)
        {
            string fileExtension = Path.GetExtension(filePath);
            string contentType = GetContentType(fileExtension);
            byte[] fileBytes = File.ReadAllBytes(filePath);

            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Compress, true))
                {
                    gzipStream.Write(fileBytes, 0, fileBytes.Length);
                }

                byte[] compressedBytes = compressedStream.ToArray();

                writer.WriteLine("HTTP/1.1 200 OK");
                writer.WriteLine($"Content-Type: {contentType}");
                writer.WriteLine("Content-Encoding: gzip");
                writer.WriteLine($"Content-Length: {compressedBytes.Length}");
                writer.WriteLine();

                stream.Write(compressedBytes, 0, compressedBytes.Length);
            }
        }

        static void ServeErrorPage(StreamWriter writer, NetworkStream stream, string fileServingPath, string errorFile)
        {
            string errorFilePath = Path.Combine(fileServingPath, errorFile);

            if (File.Exists(errorFilePath))
            {
                RedirectAndServeFile(writer, stream, errorFilePath, "/404.html");
            }
            else
            {
                writer.WriteLine("HTTP/1.1 404 Not Found");
                writer.WriteLine("Content-Type: text/html");
                writer.WriteLine("<h1>404 - Archivo no encontrado</h1>");
                writer.WriteLine();
            }
        }

        static void RedirectAndServeFile(StreamWriter writer, NetworkStream stream, string filePath, string redirectLocation)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);

            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Compress, true))
                {
                    gzipStream.Write(fileBytes, 0, fileBytes.Length);
                }

                byte[] compressedBytes = compressedStream.ToArray();

                writer.WriteLine("HTTP/1.1 302 Found");
                writer.WriteLine($"Location: {redirectLocation}");
                writer.WriteLine("Content-Type: text/html");
                writer.WriteLine("Content-Encoding: gzip");
                writer.WriteLine($"Content-Length: {compressedBytes.Length}");
                writer.WriteLine();

                stream.Write(compressedBytes, 0, compressedBytes.Length);
            }
        }

        static string GetContentType(string fileExtension)
        {
            switch (fileExtension.ToLower())
            {
                case ".html":
                    return "text/html";
                case ".css":
                    return "text/css";
                case ".js":
                    return "application/javascript";
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".gif":
                    return "image/gif";
                default:
                    return "application/octet-stream";
            }
        }

        static string HandlePostRequest(StreamReader reader, StreamWriter writer)
        {
            string contentLengthLine;
            int contentLength = 0;
            string body = "";

            while (!string.IsNullOrEmpty(contentLengthLine = reader.ReadLine()))
            {
                if (contentLengthLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    contentLength = int.Parse(contentLengthLine.Split(':')[1].Trim());
                }
            }

            if (contentLength > 0)
            {
                char[] buffer = new char[contentLength];
                reader.Read(buffer, 0, contentLength);
                body = new string(buffer);
            }

            writer.WriteLine("HTTP/1.1 201 Created");
            writer.WriteLine("Content-Type: text/plain");
            writer.WriteLine("Content-Length: 0");
            writer.WriteLine();

            return body;
        }

        static void LogRequest(string clientIp, string httpMethod, string requestedFile, string queryParams = "", string body = "")
        {
            string logFilePath = $"logs/{DateTime.Now:yyyy-MM-dd}.log";

            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            using (StreamWriter logWriter = new StreamWriter(logFilePath, true))
            {
                string logEntry = $"FECHA: {DateTime.Now:yyyy-MM-dd HH:mm:ss} - IP: {clientIp} - MÉTODO HTTP: {httpMethod}";

                if (!requestedFile.Equals("No aplica"))
                {
                    logEntry += $" - ARCHIVO: {requestedFile}";
                }

                if (!string.IsNullOrWhiteSpace(queryParams))
                {
                    string formattedParams = queryParams.Replace("&", ", ");
                    logEntry += $" - PARÁMETROS: {formattedParams}";
                }

                if (!string.IsNullOrWhiteSpace(body))
                {
                    string formattedBody = TryFormatBody(body);
                    logEntry += $" - BODY: {Environment.NewLine}{formattedBody}";
                }

                logWriter.WriteLine(logEntry.TrimEnd());
            }
        }

        static string TryFormatBody(string body)
        {
            try
            {
                JToken parsedJson = JToken.Parse(body);
                return parsedJson.ToString(Formatting.Indented);
            }
            catch (JsonReaderException)
            {
                return body.Trim();
            }
        }

        static void PrintDivider()
        {
            Console.WriteLine("-------------------------------------------------------------------------");
        }
    }
}