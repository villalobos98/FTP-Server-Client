using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace FTPServer
{
    class Server
    {
        public static readonly string[] COMMANDS = { 
                          "LIST",
                          "PASV",
                          "PORT",
                          "CD",
                          "CDUP",
                          "RETR",
                          "QUIT",
                          "USER", 
                          "TYPE"};

        public const int LIST = 0;
        public const int PASV = 1;
        public const int PORT = 2;
        public const int CD = 3;
        public const int CDUP = 4;
        public const int RETR = 5;
        public const int QUIT = 6;
        public const int USER = 7;
        public const int TYPE = 8;

        private TcpClient client;
        private NetworkStream controlStream;
        private static StreamReader inputStreamClient;
        private  static StreamWriter outputStreamClient;
        private int dataPort = 20;
        private string username;
        private string password;
        private bool debugingMode;
        private string response;
        private bool transferModeBinary = false;
        private bool transferModeASCII = false;
        private string message = null;
        private TcpClient dataSocket = null;

        String rootDirecotry = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location).ToString();

        public Server(TcpClient client)
        {
            this.client = client;
            controlStream = client.GetStream();
            inputStreamClient = new StreamReader(controlStream);
            outputStreamClient = new StreamWriter(controlStream);
        }

        public void start()
        {
            Console.WriteLine("Server is ready to send messages."); //Debugging purpose
            try
            {
                //Welcome Message
                writeMessage("220------ Welcome User-------");
                writeMessage("220-----To My FTP Server-----");
                writeMessage("220--------ServerZila--------");
                writeMessage("200--------------------------");
                while (client.Connected)
                {
                    this.response = readMessage();
                    if (!response.StartsWith("USER"))
                    {
                        writeMessage("530 Login needs USER and PASS");
                        continue;
                    }

                    int cmd = -1;
                    string[] argv = Regex.Split(response, "\\s+");
                    // What command was entered?
                    for (int i = 0; i < 13 && cmd == -1; i++)
                    {
                        if (COMMANDS[i].Equals(argv[0], StringComparison.CurrentCultureIgnoreCase))
                        {
                            cmd = i;
                        }
                    }

                    switch (cmd)
                    {
                        case USER:
                            String[] args = response.Split(" ");
                            //if (args.Length != 2)
                            //{
                            //    writeMessage("530 User name and Password required.");
                            //}
                            response = userLogin(argv[0]);
                            //if (!response.StartsWith("PASS"))
                            //{
                            //    writeMessage("530 Enter Password");
                            //}
                           
                            break;

                        case PORT:
                            setActiveMode(argv[1]);
                            break;

                        case CDUP:
                            changeToParentDirectory();
                            break;

                        case CD:
                            if (argv.Length != 2)
                            {
                                writeMessage("501 Not enough arguments. Provide command and filename.");
                            }
                            changeWorkingDirectory(argv[1]);
                            break;

                        case LIST:
                            if (dataSocket == null || !dataSocket.Connected)
                            {
                                writeMessage("530 Please use PORT or PASV.");
                            }
                            retriveDirectoryList();

                            break;
                        case RETR:
                            retrieveFile(argv);
                            break;

                        case PASV:
                            setPassiveMode();
                            break;

                        case QUIT:
                            closeServerConnection();
                            return;

                        case TYPE:
                            if (argv.Length != 2)
                            {
                                writeMessage("530 Invalid number of arguments.");
                            }
                            setTransferType(argv[1]);
                            break;

                        default:
                            message = "200 Command not supported";
                            writeMessage(message);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void retrieveFile(string[] argv)
        {
            String fileName = argv[1];
            if (argv.Length != 2)
            {
                writeMessage("501 Error with the number of arguments.");
            }
            if (File.Exists(rootDirecotry + "/" + fileName))
            {
                writeMessage("150 Opening Binary Mode connection for " + File.Open(rootDirecotry + "/" + fileName, FileMode.Open));

                StreamWriter streamWriter = new StreamWriter(dataSocket.GetStream());
                StreamReader streamReader = new StreamReader(dataSocket.GetStream());
                char[] buffer = new char[4096];
                int index = 0;
                int dataLength = -1;
                while (true)
                {
                    dataLength = streamReader.Read(buffer, index, 0);
                    if (dataLength == -1)
                    {
                        writeMessage("ERROR could not transfer the file.");
                        break;
                    }
                    streamWriter.Write(buffer, 0, index);
                }
                streamWriter.Close();
                streamReader.Close();
                writeMessage("226 File Transfer Complete.");
                dataSocket.Close();
            }
            else
            {
                writeMessage("550 Failed to open the file.");
            }
        }

        private void retriveDirectoryList()
        {
            if (dataSocket != null || dataSocket.Connected)
            {
                try
                {
                    var dirList = new DirectoryInfo(rootDirecotry).GetFiles();
                    StreamReader streamReader = new StreamReader(new BufferedStream(dataSocket.GetStream()));
                    StreamWriter streamWriter = new StreamWriter(new BufferedStream(dataSocket.GetStream()));
                    message = "150 Here comes the directory listing.";
                    writeMessage(message);
                    for (int i = 0; i < dirList.Length; i++)
                    {
                        streamWriter.Write(dirList[i]);
                    }
                    streamWriter.Flush();
                    streamWriter.Close();
                    writeMessage("[CODE] Directory Listing Sent");
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                }

            }
            // If the dataSocket is null then you must issue warning to user.
            else
            {
                writeMessage("530 You must use PORT or PASV command first.");
            }
        }

        private void setPassiveMode()
        {
            try
            {
                IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress IPAddress = ipHost.AddressList[0];
                string localIP = IPAddress.ToString();

                var message = "227 Entering Passive Mode (" + localIP.Replace('.', ',') + "," + dataPort + ")";
                TcpListener serverSocket = new TcpListener(IPAddress, dataPort);
                serverSocket.Start();
                serverSocket.AcceptTcpClient();
                writeMessage("Server is connected to data port.");
            }
            catch(Exception e)
            {
                writeMessage("520 Error Could not set pasive mode.");
            }
        }

        private void closeServerConnection()
        {
            message = "221 Goodbye.";
            writeMessage(message);
            client.Close();
        }

        private string readMessage()
        {
            var message = inputStreamClient.ReadLine();
            if (debugingMode)
            {
                Console.WriteLine("Client Message: " + message);
            }
            if(message == null)
            {
                return String.Empty;
            }
            else
            {
                return message;
            }
        }

        private void writeMessage(string message)
        {
            try
            {
                outputStreamClient.WriteLine(message);
                outputStreamClient.Flush();
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("ERROR" + e.Message);
            }

        }

        private string userLogin(string username)
        {
            var name = username.ToLower();
            string response = null;

            if (!(name.Equals("ftp")) || !name.Equals("anonymous"))
            {
                writeMessage("530 This server requires user name FTP or anonymous.");
                response = "ERROR";
            }
            writeMessage("331 Username Recieved.\nSpecify the password.");
            response = readMessage();
            if (!(response.StartsWith("PASS"))){
                writeMessage("530 You must enter a password.");
            }
            writeMessage("230 Login Successful.");
            return response;
        }

        private void changeWorkingDirectory(string fileName)
        {
            Directory.SetCurrentDirectory(fileName);
            message = "250 Changed to new Directory";
            writeMessage(message);
        }

        private void changeToParentDirectory()
        {
            var parentDirectory = Directory.GetParent(rootDirecotry).ToString();
            Directory.SetCurrentDirectory(parentDirectory);
            writeMessage("250 Changed to new Directory");
        }

        private void setTransferType(string type)
        {
            if (type.Equals("A")){
                transferModeASCII = true;
                writeMessage("200 Switching to ASCII mode.");
            }
            else if (type.Equals("I")){
                transferModeBinary = true;
                writeMessage("200 Switching to BINARY mode.");
            }
            else
            {
                writeMessage("530 Type is not supported.");
            }
        }

        private void setActiveMode(string args)
        {
            if(args.Length != 6)
            {
                Console.WriteLine("Error");
            }
            var argVector = args.Split(",");
            var ipAddress = argVector[0] + "." + argVector[1] + "." + argVector[2] + "." + argVector[3];
            int dataPort = (Int32.Parse(argVector[4]) * 256) + Int32.Parse(argVector[5]);
            dataSocket = new TcpClient(ipAddress, dataPort);
            writeMessage("200 Server Connected to the data port.");
        }

    }
}
