using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FileShare
{
    internal class Server
    {
        static void Main(string[] args)
        {
            string jsonPath = "appsettings.json";
            var json = File.ReadAllText(jsonPath);
            JObject config = JObject.Parse(json);

            string fileServingPath = config["ServerConfig"]["FileServingPath"].ToString();
            int port = (int)config["ServerConfig"]["Port"];

            TcpListener server = new TcpListener(IPAddress.Any, port);
            server.Start();
            Console.WriteLine($"Servidor de FileShare iniciado en el puerto {port}. Esperando conexiones...");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();

                ThreadPool.QueueUserWorkItem(HandleClient, new object[] { client, fileServingPath });
            }
        }

        static void HandleClient(object state)
        {
            var parameters = (object[])state;
            TcpClient client = (TcpClient)parameters[0];
            string fileServingPath = (string)parameters[1];

            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream);
            StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            try
            {
                string request = reader.ReadLine();

                string requestedFile = "index.html";

                if (!string.IsNullOrEmpty(request) && request.Contains("GET"))
                {
                    string[] parts = request.Split(' ');
                    if (parts.Length > 1 && parts[1] != "/")
                    {
                        requestedFile = parts[1].TrimStart('/');
                    }
                }

                Console.WriteLine("Solicitud recibida: " + requestedFile);

                string filePath = Path.Combine(fileServingPath, requestedFile);

                if (File.Exists(filePath))
                {
                    string fileExtension = Path.GetExtension(filePath);
                    string contentType = GetContentType(fileExtension);
                    byte[] fileBytes = File.ReadAllBytes(filePath);

                    writer.WriteLine("HTTP/1.1 200 OK");
                    writer.WriteLine($"Content-Type: {contentType}");
                    writer.WriteLine("Content-Length: " + fileBytes.Length);
                    writer.WriteLine();
                    stream.Write(fileBytes, 0, fileBytes.Length);
                    PrintDivider();
                }
                else
                {
                    string errorFilePath = Path.Combine(fileServingPath, "404.html");

                    if (File.Exists(errorFilePath))
                    {
                        string errorContent = File.ReadAllText(errorFilePath);
                        writer.WriteLine("HTTP/1.1 404 Not Found");
                        writer.WriteLine("Content-Type: text/html");
                        writer.WriteLine($"Content-Length: {errorContent.Length}");
                        writer.WriteLine();
                        writer.WriteLine(errorContent);
                        PrintDivider();
                    }
                    else
                    {
                        writer.WriteLine("HTTP/1.1 404 Not Found");
                        writer.WriteLine("Content-Type: text/html");
                        writer.WriteLine();
                        writer.WriteLine("<h1>404 - Archivo no encontrado</h1>");
                        PrintDivider();
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine("Error de conexión: " + ex.Message);
                PrintDivider();
            }
            finally
            {
                client.Close();
            }
        }

        static string GetContentType(string extension)
        {
            switch (extension.ToLower())
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

        static void PrintDivider()
        {
            Console.WriteLine("------------------------------------------------------");
        }
    }
}
