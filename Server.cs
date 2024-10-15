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
            string jsonPath = "appsettings.json";
            var json = File.ReadAllText(jsonPath);
            JObject config = JObject.Parse(json);

            string fileServingPath = config["ServerConfig"]["FileServingPath"].ToString();
            int port = (int)config["ServerConfig"]["Port"];
            TcpListener server = new TcpListener(IPAddress.Loopback, port);

            server.Start();

            Console.WriteLine($"Servidor de FileShare iniciado en el puerto {port}. Esperando conexiones... \n");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();

                ThreadPool.QueueUserWorkItem(state => HandleClient(client, fileServingPath));
            }
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
                string httpMethod = "DESCONOCIDO";
                string requestedFile = "index.html";
                string queryParams = "";
                string postData = "";

                if (!string.IsNullOrEmpty(request))
                {
                    string[] requestParts = request.Split(' ');

                    if (requestParts.Length > 0)
                    {
                        httpMethod = requestParts[0];
                    }

                    if (httpMethod == "GET")
                    {
                        string[] parts = request.Split(' ');

                        if (parts.Length > 1 && parts[1] != "/")
                        {
                            string[] fileAndParams = parts[1].Split('?');
                            requestedFile = fileAndParams[0].TrimStart('/');

                            if (fileAndParams.Length > 1)
                            {
                                queryParams = fileAndParams[1];
                            }
                        }

                        Console.WriteLine($"Archivo solicitado: {requestedFile} (IP: {clientIp}, Hilo: {Thread.CurrentThread.ManagedThreadId})");

                        string filePath = Path.Combine(fileServingPath, requestedFile);

                        if (File.Exists(filePath))
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
                                writer.WriteLine("Content-Length: " + compressedBytes.Length);
                                writer.WriteLine();

                                stream.Write(compressedBytes, 0, compressedBytes.Length);
                            }
                        }
                        else
                        {
                            string errorFilePath = Path.Combine(fileServingPath, "404.html");

                            if (File.Exists(errorFilePath))
                            {
                                byte[] errorContentBytes = File.ReadAllBytes(errorFilePath);

                                using (MemoryStream compressedStream = new MemoryStream())
                                {
                                    using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Compress, true))
                                    {
                                        gzipStream.Write(errorContentBytes, 0, errorContentBytes.Length);
                                    }

                                    byte[] compressedBytes = compressedStream.ToArray();

                                    writer.WriteLine("HTTP/1.1 302 Found");
                                    writer.WriteLine("Location: /404.html");
                                    writer.WriteLine("Content-Type: text/html");
                                    writer.WriteLine("Content-Encoding: gzip");
                                    writer.WriteLine("Content-Length: " + compressedBytes.Length);
                                    writer.WriteLine();

                                    stream.Write(compressedBytes, 0, compressedBytes.Length);
                                }
                            }
                            else
                            {
                                writer.WriteLine("HTTP/1.1 404 Not Found");
                                writer.WriteLine("Content-Type: text/html");
                                writer.WriteLine();
                                writer.WriteLine("<h1>404 - Archivo no encontrado</h1>");
                            }
                        }

                        LogRequest(clientIp, httpMethod, requestedFile, queryParams, "");
                    }
                    else if (httpMethod == "POST")
                    {
                        string contentLengthLine = "";
                        int contentLength = 0;

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
                            postData = new string(buffer);
                        }

                        writer.WriteLine("HTTP/1.1 201 Created");
                        writer.WriteLine("Content-Type: text/plain");
                        writer.WriteLine("Content-Length: 0");
                        writer.WriteLine();

                        LogRequest(clientIp, httpMethod, "No aplica", "", postData);
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error en la solicitud desde IP: {clientIp} (Hilo: {Thread.CurrentThread.ManagedThreadId}). Mensaje de error: {ex.Message}.");
            }
            finally
            {
                client.Close();

                Interlocked.Decrement(ref activeRequests);

                Console.WriteLine($"Solicitud completada de IP: {clientIp}. Hilo: {Thread.CurrentThread.ManagedThreadId}. Solicitudes activas: {activeRequests}");

                PrintDivider();
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

        static void LogRequest(string clientIp, string httpMethod, string requestedFile, string queryParams = "", string postData = "")
        {
            string logFilePath = $"logs/{DateTime.Now:yyyy-MM-dd}.log";

            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            using (StreamWriter logWriter = new StreamWriter(logFilePath, true))
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - IP: {clientIp} - Método: {httpMethod} - Solicitud: {requestedFile}";

                if (!string.IsNullOrWhiteSpace(queryParams))
                {
                    string formattedParams = queryParams.Replace("&", ", ");
                    logEntry += $" - Parámetros: {formattedParams}";
                }

                if (!string.IsNullOrWhiteSpace(postData))
                {
                    string formattedPostData;

                    try
                    {
                        var parsedJson = JToken.Parse(postData);
                        
                        formattedPostData = parsedJson.ToString(Formatting.Indented);
                    }
                    catch (JsonReaderException)
                    {
                        formattedPostData = postData.Trim();
                    }

                    logEntry += $" - Body: {Environment.NewLine}{formattedPostData}";
                }

                logWriter.WriteLine(logEntry.TrimEnd());
            }
        }

        static void PrintDivider()
        {
            Console.WriteLine("-------------------------------------------------------------------------");
        }
    }
}