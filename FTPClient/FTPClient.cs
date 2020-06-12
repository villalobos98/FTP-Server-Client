
/*
@Author:      Isaias Villalobos
@Description: This class represents the client-side for implementation of FTP project.
              This is the driver for the client. It uses the Methods class to 
              properly implement the User Entered Commands.'
@date:        5/29/2020
@version:     1.1 -- Some basic commands were implemented

*/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace FTP
{
    class Ftp
    {
        // This variable is for the debugging mode
        static int port = 21;

        // The prompt
        public const string PROMPT = "FTP> ";

        // Information to parse commands
        public static readonly string[] COMMANDS = { "ascii",
                          "binary",
                          "cd",
                          "cdup",
                          "debug",
                          "dir",
                          "get",
                          "help",
                          "passive",
                          "pwd",
                          "quit",
                          "user" };

        public const int ASCII = 0;
        public const int BINARY = 1;
        public const int CD = 2;
        public const int CDUP = 3;
        public const int DEBUG = 4;
        public const int DIR = 5;
        public const int GET = 6;
        public const int HELP = 7;
        public const int PASSIVE = 8;
        public const int PWD = 9;
        public const int QUIT = 10;
        public const int USER = 11;

        // Help message

        public static readonly String[] HELP_MESSAGE = {
            "ascii      --> Set ASCII transfer type",
            "binary     --> Set binary transfer type",
            "cd <path>  --> Change the remote working directory",
            "cdup       --> Change the remote working directory to the",
                "               parent directory (i.e., cd ..)",
            "debug      --> Toggle debug mode",
            "dir        --> List the contents of the remote directory",
            "get path   --> Get a remote file",
            "help       --> Displays this text",
            "passive    --> Toggle passive/active mode",
            "pwd        --> Print the working directory on the server",
            "quit       --> Close the connection to the server and terminate",
            "user login --> Specify the user name (will prompt for password" };

        // Variables needed to handled I/O to the server
        private static TcpClient client;
        private static StreamReader reader;
        private static StreamWriter writer;
        private static Socket socket;
        private static Socket dSocket;
        private static TcpClient dataSocket;
        private static bool loggedIn = false;
        private static bool debuggingMode = false;
        private static bool isPassive;
        private static int dataPort = Math.Abs(new Random().Next(0,2000)) + 1024;
        private static bool inBinaryMode = false;
        private static bool inASCIIMode = true;


        public static void Main(string[] args)
        {
            bool eof = false;
            String input = null;
            string hostName;

            // Handle the command line error messages.
            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine("Usage: FTP server <optional port>");
                return;
            }

            //Set the HostName
            hostName = args[0];


            //When exactly 2 arguments appear then arg[0] is host and arg[1] is port
            if (args.Length == 2)
            {
                port = Int32.Parse(args[1]);
                // Check if the port is valid.
                if (port < 0)
                {
                    Console.WriteLine(port.ToString() + " is not in valid range");
                }
            }

            //Start the server connection;
            try
            {
                startConnection(hostName, port);
            }
            catch (IOException e)
            {
                Console.WriteLine("Connection to the server failed." + e.Message);
                return;
            }

            //This must be done first.
            Console.WriteLine("You must enter USER command to login.");

            // Command line is done - accept commands
            do
            {
                try
                {
                    Console.Write(PROMPT);
                    input = Console.ReadLine();
                }
                catch (Exception e)
                {
                    eof = true;
                    Console.WriteLine(e.Message);
                }

                // Keep going if we have not hit end of file
                if (!eof && input.Length > 0)
                {
                    int cmd = -1;
                    string[] argv = Regex.Split(input, "\\s+");

                    // What command was entered?
                    for (int i = 0; i < 13 && cmd == -1; i++)
                    {
                        if (COMMANDS[i].Equals(argv[0], StringComparison.CurrentCultureIgnoreCase))
                        {
                            cmd = i;
                        }
                    }

                    // Execute the command
                    switch (cmd)
                    {
                        case ASCII:
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: ASCII Mode Toggled");
                            }
                            setASCIITransferType();
                            inASCIIMode = true;
                            break;

                        case BINARY:
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: Binary Mode Toggled");
                            }
                            setBinaryTransferType();
                            inBinaryMode = true;
                            break;

                        case CD:
                            if (args.Length == 1)
                            {
                                Console.WriteLine("Usage: CD path");
                                return;
                            }
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: Change Directory Toggled");
                            }
                            var dir = argv[1];
                            changeDirectory(dir);
                            break;

                        case CDUP:
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: Change Directory Parent Toggled");
                            }

                            getParentDirectory();
                            break;

                        case DEBUG:
                            var methods = new ClientMethods();
                            debuggingMode = methods.setDebugUser(debuggingMode);

                            if (debuggingMode)
                            {
                                Console.WriteLine("Debugging Mode Toggled");
                            }
                            break;

                        case DIR:
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: List the contents of the remote system");
                            }
                            listDirectores(isPassive);
                            break;

                        case GET:
                            if (args.Length == 1 || args.Length > 2)
                            {
                                Console.WriteLine("Usage: GET filename");
                                return;
                            }
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: Retrieving a file from the remote system");
                            }
                            if (args.Length == 2)
                            {
                                var fileName = argv[1];
                                retreiveFromServer(fileName);
                            }
                            else
                            {
                                Console.WriteLine("Usage: Something went wrong.");
                            }
                            break;

                        case HELP:
                            for (int i = 0; i < HELP_MESSAGE.Length; i++)
                            {
                                Console.WriteLine(HELP_MESSAGE[i]);
                            }
                            break;

                        case PASSIVE:
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: Passive mode enabled.");
                            }
                            setPassiveTransferMode();
                            isPassive = true;
                            break;

                        case PWD:
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: Printing a list of directories.");
                            }
                            printWorkingDirectory();
                            break;

                        case USER:
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: User debugging mode enabled.");
                            }

                            if (argv.Length != 2)
                                Console.WriteLine("Usage: USER username");
                            else
                            {
                                var username = argv[1];
                                userLogin(username, hostName, port);
                            }
                            break;

                        case QUIT:
                            if (debuggingMode)
                            {
                                Console.WriteLine("DEBUG: Terminating program and closing connection.");
                            }
                            eof = true;
                            //Close connection to tcpclient.
                            closeConnection();
                            break;

                        default:
                            Console.WriteLine("Invalid command");
                            break;
                    }
                }
            } while (!eof);
        }



        public static void getParentDirectory()
        {
            try
            {
                send("CDUP");
                var response = read();
                if (response.StartsWith("421"))
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

        //Supposed to be working, error when passive mode is set and server send back 0,0,0,0, XX, XX
        public static void listDirectores(bool isPassive)
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

                    StreamReader dataReader = new StreamReader(dataSocket.GetStream());

                    response = read();

                    if (!response.StartsWith("150"))
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
                catch (Exception e)
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

        //Always working
        private static void startConnection(string hostName, int port)
        {
            client = new TcpClient();
            client.Client.Connect(hostName, port);
            Console.WriteLine("Connected to " + hostName + ".");

            /* Getting the IO streams. */
            reader = new StreamReader(client.GetStream());
            writer = new StreamWriter(client.GetStream());

            Console.WriteLine(reader.ReadLine());
            /* Loading the welcome message */
            while (true)
            {
                String response = read();
                Console.WriteLine(response);
                if (response.StartsWith("200"))
                {
                    break;
                }
            }
        }

        //Should be nothing wrong with this
        public static string read()
        {
            string message = null;
            try
            {
                message = reader.ReadLine();
                if (debuggingMode)
                {
                    Console.WriteLine("Message from the server: " + message);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return message;
        }

        //Should be nothing wrong with this
        public static void send(String message)
        {
            try
            {

                writer.WriteLine(message);
                writer.Flush();
            }
            catch (IOException e)
            {
                Console.WriteLine("IO Error: " + e.Message);
                return;
            }
        }

        //working
        public static void userLogin(string user, string hostName, int port)
        {
            try
            {

                Console.Write("Name(" + hostName + ":" + user + "): ");

                //Read username from console
                var username = Console.ReadLine();

                //Send username to server
                send("USER " + username);
                var response = read();
                Console.WriteLine(response);

                // Check to see if there is some issue with the username and throw exception
                if (response.IndexOf("331", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new Exception(string.Format("Error \"{0}\" while sending user name \"{1}\".", response, username));

                // This will handle the "Password" part of the user.
                Console.Write("Specify Password: ");
                var password = Console.ReadLine();

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

        //working
        public static void closeConnection()
        {
            // Check if the "client" is connected to the "server"
            send("quit");
            var response = read();
            if(!response.StartsWith("221"))
            {
                Console.WriteLine("Error with closing connection.");
            }

            client.Close();
            Console.WriteLine(response);
            return;
        }
        //working
        public static void printWorkingDirectory()
        {
            send("PWD");
            var response = read();
            if (response.StartsWith("257"))
            {
                int startingIndex = response.IndexOf("\"");
                //Console.WriteLine("Current Directory is: " + response.Substring(startingIndex));
                Console.WriteLine(response);
            }
            else
            {
                Console.WriteLine("ERROR with printing working directory.");
            }
        }

        //NOT WORKING
        public static void setActiveTransferMode()
        {
            dataPort++;
            int upperDataPort = dataPort / 256;
            int lowerDataPort = dataPort % 256;

            // The default was using IPv6 local IP, note to self
            IPHostEntry localHost = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress localIP = localHost.AddressList[4];
            IPEndPoint localEndPoint = new IPEndPoint(localIP, dataPort);
            string message = "PORT " + localIP.ToString().Replace('.', ',') + "," +  lowerDataPort + "," + lowerDataPort;
            TcpClient socket = new TcpClient();
            //socket.Client.Bind(localEndPoint);
            send(message);
            //dSocket = socket.Client.Accept();
            var response = read();
            Console.WriteLine(response);
            //if (!response.StartsWith("200"))
            //{
            //    throw new Exception("Server could not be started." + response);
            //}
        }

        //WORKING
        public static void setPassiveTransferMode()
        {
            try
            {
                String response;
                send("PASV");
                response = read();
                Console.WriteLine(response);

                if (!response.StartsWith("227"))
                {
                    throw new IOException("Could not request passive mode: \n" + response);
                }

                int index1 = response.IndexOf("(");
                int index2 = response.IndexOf(")");
                var length = index2 - index1;
                String dl = response.Substring(index1 + 1, length - 1);
                var arr = dl.Split(",");
                string ipString = arr[0] + "." + arr[1] + "." + arr[2] + "." + arr[3];
                IPAddress address = IPAddress.Parse(ipString);
                int port = Int32.Parse(arr[4]) * 256 + Int32.Parse(arr[5]);

                IPAddress ipAddr = IPAddress.Parse(ipString);
                IPEndPoint remoteEndPoint = new IPEndPoint(ipAddr, port);

                //This will be the data communciation socket
                dataSocket = new TcpClient(ipString, port);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not enter passive mode. " + e.Message);
            }
        }

        public static void retreiveFromServer(string filename)
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

        public static void changeDirectory(string filename)
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

        //working
        public static void setASCIITransferType()
        {
            try
            {
                send("TYPE I");
                var response = read();
                Console.WriteLine(response); //debug
                if (!response.StartsWith("200 "))
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
        //working
        public static void setBinaryTransferType()
        {
            try
            {
                send("TYPE I");
                var response = read();
                Console.WriteLine(response);
                if (!response.StartsWith("200 "))
                {
                    Console.WriteLine("ERROR" + response);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}