/*
    @name:        FTPServer.
    @author:      Isaias Villalobos
    @description: This class is the main driver program for the FTP server
                  it uses the Server classes to help create and serve clients
*/
using FTPServer;
using System;
using System.Net;
using System.Net.Sockets;

namespace FTP
{
    public class FTPServer
    {
        private static int port = 2121;
        private static TcpListener serverSocket;
        private static Server server;

        public static void Main(String[] args)
        {
            startServer();
        }

        private static void startServer()
        {
            try
            {
                serverSocket = new TcpListener(IPAddress.Any, port);
                serverSocket.Start();

                while (true)
                {
                    var tcpClient = serverSocket.AcceptTcpClient();
                    server = new Server(tcpClient);
                    server.start();
                }
            }
            catch (SocketException s)
            {
                Console.Error.WriteLine("SOKCET EXCEPTION" + s.Message);
            }
            catch (ArgumentNullException n)
            {
                Console.Error.WriteLine("NUll" + n.Message);
            }
            catch (ArgumentException a)
            {
                Console.Error.WriteLine("ARUGMENT NULL" + a.Message);
            }
            catch (Exception e)
            {
                //This is to handle other exceptions that were not seen.
                Console.Error.WriteLine("EXCEPTION THROWN" + e.Message);
            }

        }
    }
}