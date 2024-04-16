using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new(IPAddress.Any, 4221);
server.Start();

while (true)
{
    Console.WriteLine("Waiting for new connection...");
    Socket socket = server.AcceptSocket(); // wait for client

    Task client = new(() => HandleClient(socket, args));
    client.Start();
}

static bool HasDirectory(string[] args, out string directory)
{
    const string directoryFlag = "--directory";
    directory = "";

    int directoryFlagIndex = Array.IndexOf(args, directoryFlag);
    int directoryArgumentPosition = directoryFlagIndex + 1;
    bool hasDirectory = directoryFlagIndex != -1 && args.Length > directoryArgumentPosition;

    if (hasDirectory)
    {
        directory = args[directoryArgumentPosition];
    }

    return hasDirectory;
}

static void HandleClient(Socket socket, string[] args)
{
    bool hasDirectory = HasDirectory(args, out string directory);

    Console.WriteLine($"Connection Accepted :: {socket.RemoteEndPoint} ::");

    string response200OK = "HTTP/1.1 200 OK";
    string response201Created = "HTTP/1.1 201 Created";
    string response404NotFound = "HTTP/1.1 404 Not Found";

    string contentType = "\r\nContent-Type: text/plain";

    byte[] requestReceived = new Byte[1024];
    int bytesReceived = socket.Receive(requestReceived);
    string request = Encoding.ASCII.GetString(requestReceived, 0, bytesReceived);
    Console.WriteLine($"Request:\r\n{request}");

    string[] requestLines = request.Split("\r\n");
    string contentRequest = requestLines.Last();

    string httpMethod = "";
    string path = "";
    string httpVersion = "";

    if (requestLines.Length > 0)
    {
        string[] startLineSplit = requestLines[0].Split(' ');

        if (startLineSplit.Length >= 3)
        {
            httpMethod = startLineSplit[0];
            path = startLineSplit[1];
            httpVersion = startLineSplit[2];
        }
    }

    string response = response404NotFound;
    string basePath = "/echo/";
    string content = "";
    string headers = "";

    if (path.Contains(basePath))
    {
        response = response200OK;
        content = path.Replace(basePath, "");
    }
    else if (path.Equals("/"))
    {
        response = response200OK;
    }
    else if (path.Contains("/user-agent"))
    {
        response = response200OK;
        string userAgentKey = "User-Agent: ";
        string? userAgent = requestLines.FirstOrDefault(rl => rl.Contains(userAgentKey));
        if (userAgent != null)
        {
            content = userAgent.Substring(userAgentKey.Length);
        }
    }
    else if (path.Contains("/files/") && hasDirectory && httpMethod.Equals("GET"))
    {
        string fileName = path["/files/".Length..];
        string filePath = $"{directory}{fileName}";

        if (File.Exists(filePath))
        {
            response = response200OK;
            content = File.ReadAllText(filePath);
        }
        contentType = "\r\nContent-Type: application/octet-stream";

    }
    else if (path.Contains("/files/") && hasDirectory && httpMethod.Equals("POST"))
    {
        string fileName = path["/files/".Length..];
        string filePath = $"{directory}{fileName}";

        File.WriteAllText(filePath, contentRequest);

        response = response201Created;
        contentType = "";

    }

    if (content.Length > 0)
    {
        headers = contentType;
        headers += $"\r\nContent-Length: {content.Length}";
    }

    response = $"{response}{headers}\r\n\r\n{content}";

    Console.WriteLine($"Response:\r\n{response}");

    byte[] socketReponse = Encoding.ASCII.GetBytes(response);

    socket.Send(socketReponse);
    socket.Close();
}
