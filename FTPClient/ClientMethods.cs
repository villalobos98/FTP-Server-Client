using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

public class ClientMethods
{
    private TcpClient client = new TcpClient();
    static int commandPort = 21;
    static int dataPort = 21;
    private static NetworkStream networkStream;
    private static Socket socket;
    private static Socket dataSocket = null;
    private static bool passive = true;
    private static bool modeBinary = true;
    static byte[] buffer = new byte[4096];
    static byte[] data = new byte[4096];

    string username;
    string password;

    public void setASCIITransferType()
    {
        try
        {
            send("TYPE ASCII");
            var response = read();
            Console.WriteLine(response); //debug
            if (response.StartsWith("200 "))
            {
                Console.WriteLine("Switching to ASCII mode");
            }
            else
            {
                Console.WriteLine("ERROR" + response);
            }
        }
        catch (Exception e)
        {
            // Catch a more specific exception later on
            Console.WriteLine(e.Message);
        }
    }

    public void setBinaryTransferType()
    {
        try
        {
            send("TYPE BINARY");
            var response = read();
            Console.WriteLine(response);
            if (response.StartsWith("200 "))
            {
                Console.WriteLine("Switching to Binary mode.");
            }
            else
            {
                Console.WriteLine("ERROR" + response);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public void changeDirectory(string filename)
    {
        try
        {
            send("CWD" + filename);
            var response = read();
            if (response.StartsWith("250 "))
            {
                Console.WriteLine("Directory successfully changed.");
            }
            else
            {
                Console.WriteLine("ERROR" + response);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public void getParentDirectory()
    {
        try
        {
            //Command Connection
            send("CDUP");
            var response = read();
            //Sucessfi;;y 
            if (response.StartsWith("421 "))
            {
                Console.WriteLine("Current directory moved to parent directory.");
            }
            else
            {
                Console.WriteLine(response);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public void retreiveFromServer(string filename)
    {
        send("RETR" + filename);
        var response = read();
        if (response.StartsWith("150 "))
        {
            Console.WriteLine("Command Successful");
        }
        else
        {
            Console.WriteLine("Command Not Succesful");
        }
    }

    public void listDirectores(bool isPassive)
    {
        String response;
        if (isPassive)
        {
            setPassiveTransferMode();
        }
        else
        {
            setActiveTransferMode();
        }
        try
        {
            send("LIST");
            try
            {
                TextReader dataReader = new StreamReader(new NetworkStream(dataSocket));
                response = read();

                if (!response.StartsWith("150 "))
                {
                    Console.WriteLine("Could not get the directory listing:" + response);
                }
                while ((response = dataReader.ReadLine()) != null)
                {
                    Console.WriteLine(response);
                }
                dataReader.Close();
                dataSocket.Close();
            }
            catch (IOException e)
            {
                Console.WriteLine("Could not get the directory listing: Data" + "Socket error:" + e.Message);
            }

            response = read();
            if (!response.StartsWith("226 "))
            {
                Console.WriteLine("Directory send failed: " + response);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
    //Working
    public bool setDebugUser(bool debuggingMode)
    {
        if (debuggingMode == false)
            return debuggingMode = true;
        else
            return debuggingMode = false;
    }

    //Not tested
    public void setPassiveTransferMode()
    {
        String response;
        send("PASV");
        response = read();
        Console.WriteLine(response);
        if (!response.StartsWith("227 "))
        {
            throw new IOException("Could not request passive mode: " + response);
        }
        int index1 = response.IndexOf("(");
        int index2 = response.IndexOf(")", index1 + 1);
        String dl = response.Substring(index1 + 1, index2);
        var tokens = Regex.Split(dl, ",");
        Console.WriteLine(tokens);

    }
    //Not tested
    public void setActiveTransferMode()
    {

        IPHostEntry localHost = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress localIP = localHost.AddressList[0];
        IPEndPoint localEndPoint = new IPEndPoint(localIP, dataPort); //data port is

        Socket socket = new Socket(localIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(localEndPoint);
        string message = "PORT";
        send(message);
        dataSocket = socket.Accept();
        var response = read();
        if (response.StartsWith("200 "))
        {
            Console.WriteLine("OK");
        }
        else
        {
            Console.WriteLine("Active mode not set");
        }
    }

    //Working
    public void printWorkingDirectory()
    {
        send("PWD");
        var response = read();
        if (response.StartsWith("257 "))
        {
            int startingIndex = response.IndexOf("\"");
            Console.WriteLine("Current Directory is: " + response.Substring(startingIndex));
        }
        else
        {
            Console.WriteLine("Error" + response);
        }

    }

    //Working
    public void closeConnection()
    {
        // Check if the "client" is connected to the "server"
        client.Close();
        Console.WriteLine("Closing connection");
        return;
    }

    //Working, sanatize input
    public void userLogin(string hostName, int port)
    {
        try
        {

            //Read username from console
            username = Console.ReadLine();

            //Send username to server
            send("USER " + username);
            var response = read();
            Console.WriteLine(response);

            // Check to see if there is some issue with the username and throw exception
            if (response.IndexOf("331", StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception(string.Format("Error \"{0}\" while sending user name \"{1}\".", response, username));

            // This will handle the "Password" part of the user.
            Console.WriteLine("Specify Password: ");
            password = Console.ReadLine();

            // Send the password to the server.
            send("PASS " + password);
            response = read();

            // Check if there is some issue with the password and throw exception.
            if (response.IndexOf("230", StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception(string.Format("Error \"{0}\" while sending password.", response));

            Console.WriteLine(response);

            return;

        }
        catch (Exception e)
        {
            // Change the exception handle specific user name exception and print out message
            // For now the code will catch the exception 'e' and print out the message relevant to it
            Console.WriteLine(e.Message);
            return;
        }

    }

    // Works used in userLogin
    public void connectToServer(TcpClient tcpClient, string hostName)
    {

        // only handles ASCII right now, change to binary later.
        networkStream = tcpClient.GetStream();
        if (!networkStream.CanWrite || !networkStream.CanRead)
            return;

        var streamReader = new StreamReader(networkStream);
        while (true)
        {
            string line = streamReader.ReadLine();
            if (!line.StartsWith("220"))
            {
                Console.WriteLine("Unknown Response when connecting to the server.");
                return;
            }
            Console.WriteLine(line);
            if (line.StartsWith("220 "))
            {
                break;
            }
        }
        Console.Write("Name " + "(" + hostName + ":" + username + "):");
    }

    //Should be working, nothing broken so far
    public string Flush(TcpClient tcpClient)
    {
        try
        {
            var networkStream = tcpClient.GetStream();
            if (!networkStream.CanWrite || !networkStream.CanRead)
                return string.Empty;

            var receiveBytes = new byte[tcpClient.ReceiveBufferSize];

            // Timeout after 10,000 ms
            networkStream.ReadTimeout = 10000;
            networkStream.Read(receiveBytes, 0, tcpClient.ReceiveBufferSize);

            return Encoding.UTF8.GetString(receiveBytes);
        }
        catch
        {
            // Catch all;
        }

        return string.Empty;
    }

    //This is working
    public string read()
    {
        string message = null;
        try
        {
            networkStream.Read(data, 0, data.Length);
            message = Encoding.ASCII.GetString(data);

        }
        catch (IOException e)
        {
            Console.WriteLine("IO Error: " + e.Message);
        }
        return message;
    }

    //This is working, testing on sample connection to ftp.us.debian.org
    public void send(String message)
    {
        try
        {
            var data = Encoding.ASCII.GetBytes(message + "\r\n");
            networkStream.Write(data, 0, data.Length);
            networkStream.Flush();
        }
        catch (IOException e)
        {
            Console.WriteLine("IO Error: " + e.Message);
            return;
        }
    }
}