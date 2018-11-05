using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InWorldz.Whip.Client;
using System.Security.Cryptography;
using System.Net.Sockets;
using System.Net;

namespace whipstress
{
    class Program
    {
        static private AssetImport _import;
        private static MsSqlAssetImport _msimport;
        static private string _lastRandom;

        static private Socket _meshSignalSocket = new Socket(AddressFamily.InterNetwork,
                   SocketType.Dgram, ProtocolType.Udp);

        static private byte[] _signal = new byte[128];

        static private EndPoint _ep = new IPEndPoint(IPAddress.Any, 0);

        static void ProcessMeshSignal(IAsyncResult result)
        {
            _meshSignalSocket.BeginReceiveFrom(_signal, 0, 128, SocketFlags.None, ref _ep, new AsyncCallback(Program.ProcessMeshSignal), new object());
        }

        static RemoteServer ConnectServerByConsole()
        {
            Console.Write("Server: ");
            string host = Console.ReadLine();
            Console.WriteLine();
            Console.Write("Port: ");
            ushort port = Convert.ToUInt16(Console.ReadLine());
            Console.WriteLine();
            Console.Write("Password: ");
            string password = Console.ReadLine();
            Console.WriteLine();

            RemoteServer server = new RemoteServer(host, port, password);
            server.Start();

            return server;
        }

        static RemoteServer[] ConnectServerByConsoleX(int count)
        {
            Console.Write("Server: ");
            string host = Console.ReadLine();
            Console.WriteLine();
            Console.Write("Port: ");
            ushort port = Convert.ToUInt16(Console.ReadLine());
            Console.WriteLine();
            Console.Write("Password: ");
            string password = Console.ReadLine();
            Console.WriteLine();

            RemoteServer[] servers = new RemoteServer[count];

            for (int i = 0; i < count; i++)
            {
                RemoteServer server = new RemoteServer(host, port, password);
                server.Start();

                servers[i] = server;
            }

            return servers;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("WHIP test suite");

            try
            {
                RemoteServer server = ConnectServerByConsole();

                /*IPEndPoint listenEndPoint = new IPEndPoint(IPAddress.Any, 32702);
                _meshSignalSocket.Bind(listenEndPoint);

                _meshSignalSocket.BeginReceiveFrom(_signal, 0, 128, SocketFlags.None, ref _ep, new AsyncCallback(Program.ProcessMeshSignal), new object());
                */
                OnConnection(server);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error connecting to server: " + e.Message);
            }

            Console.ReadLine();
        }

        static void OnConnection(RemoteServer server)
        {

            while (true)
            {
                try
                {
                    Console.Write("> ");
                    ExecCmd(server, Console.ReadLine());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception during operation: " + e.Message);
                }
            }


        }


        static void ExecCmd(RemoteServer server, string cmd)
        {
            if (cmd == "iwim_heartbeat")
            {
                Socket sendSocket = new Socket(AddressFamily.InterNetwork,
                   SocketType.Dgram, ProtocolType.Udp);

                sendSocket.Bind(new IPEndPoint(IPAddress.Any, 32702));

                Console.WriteLine("Sending heartbeat to server");
                IPAddress sendTo = Dns.GetHostAddresses(server.HostName)[0];
                IPEndPoint sendEndPoint = new IPEndPoint(sendTo, 32701);


                byte[] buffer = new byte[5];
                buffer[0] = 2;
                buffer[1] = 0xFF;
                buffer[2] = 0xFF;
                buffer[3] = 0xFF;
                buffer[4] = 0xFF;

                sendSocket.SendTo(buffer, 5, SocketFlags.None, sendEndPoint);
                sendSocket.Close();
            }

            if (cmd == "iwim_query_random" || cmd == "iwim_query_random_loop")
            {
                Socket sendSocket = new Socket(AddressFamily.InterNetwork,
                    SocketType.Dgram, ProtocolType.Udp);

                int loops;

                if (cmd == "iwim_query_random")
                {
                    loops = 1;
                }
                else
                {
                    loops = 1000;
                }

                Console.WriteLine("Searching for {0} random assets starting {1}", loops, DateTime.Now);
                IPAddress sendTo = Dns.GetHostAddresses(server.HostName)[0];
                IPEndPoint sendEndPoint = new IPEndPoint(sendTo, 32701);
                byte[] buffer = new byte[33];
                buffer[0] = 0;

                for (int i = 0; i < loops; i++)
                {

                    Guid guid = Guid.NewGuid();
                    string fguid = guid.ToString().Replace("-", "");
                    Array.Copy(Util.UuidToAscii(fguid), 0, buffer, 1, 32);

                    sendSocket.SendTo(buffer, 33, SocketFlags.None, sendEndPoint);

                    EndPoint recvEndpoint = new IPEndPoint(IPAddress.Any, sendEndPoint.Port);
                    byte[] response = new byte[34];
                    sendSocket.ReceiveFrom(response, ref recvEndpoint);
                }

                Console.WriteLine("Done {0}", DateTime.Now);
            }

            if (cmd.Length > 10 && cmd.Substring(0, 10) == "iwim_query")
            {
                string uuid = cmd.Substring(11);

                Console.WriteLine("Asking for named asset " + uuid);
                server.GetAsset(uuid);
            }

            if (cmd == "getrandom")
            {
                Guid guid = Guid.NewGuid();
                Console.WriteLine("Asking for random asset " + guid.ToString());
                server.GetAsset(guid.ToString());
            }

            if (cmd.Length > 11 && cmd.Substring(0, 11) == "randomforce")
            {
                string begUUID = cmd.Substring(12);
                Guid guid = Guid.NewGuid();
                string newGuid = guid.ToString();
                newGuid = begUUID + newGuid.Substring(3);

                Console.WriteLine("Asking for semirandom asset " + newGuid);
                server.GetAsset(newGuid);
            }

            if (cmd.Length > 10 && cmd.Substring(0, 10) == "srandomput")
            {
                string begUUID = cmd.Substring(11);
                Guid guid = Guid.NewGuid();
                string newGuid = guid.ToString();
                newGuid = begUUID + newGuid.Substring(3);

                Console.WriteLine("Putting semirandom asset " + newGuid);

                Asset asset = new Asset(newGuid, 1,
                        false, false, 0, "Random Asset", "Radom Asset Desc", TestUtil.RandomBytes());
                server.PutAsset(asset);
            }

            if (cmd.Length > 6 && cmd.Substring(0, 6) == "getone")
            {
                string uuid = cmd.Substring(7);

                Console.WriteLine("Asking for named asset " + uuid);
                Asset asset = server.GetAsset(uuid);

                using (System.IO.FileStream outstream = System.IO.File.OpenWrite("asset.txt"))
                {
                    outstream.Write(asset.Data, 0, asset.Data.Length);
                    outstream.Close();
                }
            }

            if (cmd == "put")
            {
                Console.WriteLine("Putting 1000 random assets to server");
                Console.WriteLine(DateTime.Now);
                for (int i = 0; i < 1000; i++)
                {
                    Asset asset = new Asset(OpenMetaverse.UUID.Random().ToString(), 1,
                        false, false, 0, "Random Asset", "Radom Asset Desc", TestUtil.RandomBytes());
                    server.PutAsset(asset);
                }
                Console.WriteLine("Done: " + DateTime.Now);
            }

            if (cmd == "putone")
            {
                string uuid = OpenMetaverse.UUID.Random().ToString();
                _lastRandom = uuid;

                Console.WriteLine("Putting 1 random asset to server");
                Console.WriteLine(uuid);
                Console.WriteLine(DateTime.Now);

                Asset asset = new Asset(uuid, 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", TestUtil.RandomBytes());
                server.PutAsset(asset);

                Console.WriteLine("Done: " + DateTime.Now);
            }

            if (cmd == "repeatput")
            {
                Console.WriteLine("Reputting random asset to server");
                Console.WriteLine(_lastRandom);
                Console.WriteLine(DateTime.Now);

                Asset asset = new Asset(_lastRandom, 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", TestUtil.RandomBytes());
                server.PutAsset(asset);

                Console.WriteLine("Done: " + DateTime.Now);
            }

            if (cmd == "verify")
            {
                Dictionary<string, byte[]> uuids = new Dictionary<string, byte[]>();

                Console.WriteLine("Putting 1000 random 100K assets to server");
                Console.WriteLine(DateTime.Now);

                SHA1 sha = new SHA1CryptoServiceProvider();


                for (int i = 0; i < 1000; i++)
                {
                    string uuidstr = OpenMetaverse.UUID.Random().ToString();
                    byte[] randomBytes = TestUtil.RandomBytes();
                    byte[] challengeHash = sha.ComputeHash(randomBytes);
                    uuids.Add(uuidstr, challengeHash);

                    Asset asset = new Asset(uuidstr, 1,
                        false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);
                    server.PutAsset(asset);
                }
                Console.WriteLine("Done: " + DateTime.Now);
                Console.WriteLine("Rereading written assets");
                Console.WriteLine(DateTime.Now);

                foreach (KeyValuePair<string, byte[]> kvp in uuids)
                {
                    Asset a = server.GetAsset(kvp.Key);
                    byte[] hash = sha.ComputeHash(a.Data);
                    if (!TestUtil.Test.test(hash, kvp.Value))
                    {
                        Console.WriteLine("Mismatched hash on " + kvp.Key);
                        Console.WriteLine("Got " + Util.HashToHex(hash) + " expected " + Util.HashToHex(kvp.Value));

                        ASCIIEncoding encoding = new ASCIIEncoding();

                        Console.WriteLine("Data " + encoding.GetString(a.Data));

                    }
                }

                Console.WriteLine("finished verifing assets");
                Console.WriteLine(DateTime.Now);
            }

            if (cmd == "purgelocals")
            {
                server.MaintPurgeLocals();
            }

            if (cmd == "thread")
            {
                Console.WriteLine("Starting heavy thread test");
                ThreadTest test = new ThreadTest(server, false);
                test.Start();
            }

            if (cmd == "thread_purgelocal")
            {
                Console.WriteLine("Starting heavy thread test");
                ThreadTest test = new ThreadTest(server, true);
                test.Start();
            }

            if (cmd == "threadmesh")
            {
                RemoteServer server2 = ConnectServerByConsole();

                Console.WriteLine("Starting heavy thread mesh test");
                CrossServerThreadTest test = new CrossServerThreadTest(server, server2);
                test.Start();
            }

            if (cmd == "threadmesh3")
            {
                RemoteServer server2 = ConnectServerByConsole();
                RemoteServer server3 = ConnectServerByConsole();

                Console.WriteLine("Starting heavy thread mesh test");
                CrossServerThreadTest3 test = new CrossServerThreadTest3(server, server2, server3);
                test.Start();
            }

            if (cmd == "testmulticonn")
            {
                RemoteServer[] server1 = ConnectServerByConsoleX(10);
                RemoteServer[] server2 = ConnectServerByConsoleX(10);
                RemoteServer[] server3 = ConnectServerByConsoleX(10);

                List<RemoteServer> allServers = new List<RemoteServer>();
                allServers.AddRange(server1);
                allServers.AddRange(server2);
                allServers.AddRange(server3);

                CrossServerTestMulticonn test = new CrossServerTestMulticonn(server1[0], server2[0], server3[0], allServers.ToArray(), true);
                test.Start();
            }

            if (cmd == "testmulticonnsingle")
            {
                RemoteServer[] server1 = ConnectServerByConsoleX(10);

                CrossServerTestMulticonn test = new CrossServerTestMulticonn(server1[0], server1[0], server1[0], server1, false);
                test.Start();
            }

            if (cmd == "meshbias3")
            {
                RemoteServer[] servers2 = ConnectServerByConsoleX(10);
                RemoteServer server3 = ConnectServerByConsole();

                Console.WriteLine("Starting heavy thread biased mesh test");
                CrossServerBiasTest3 test = new CrossServerBiasTest3(server, servers2, server3);
                test.Start();
            }

            if (cmd == "connect")
            {
                //fast connect and disconnect to try and kill the whip service
                for (int i = 0; i < 1000; i++)
                {
                    server.Stop();
                    server.Start();
                }
            }

            if (cmd == "status")
            {
                Console.WriteLine(server.GetServerStatus());
            }

            if (cmd == "prefix")
            {
                Console.WriteLine(server.GetAssetIds("00000000000000000000000000000000"));
            }

            if (cmd == "compare")
            {
                RemoteServer server1 = ConnectServerByConsole();
                RemoteServer server2 = ConnectServerByConsole();
                RunCompare(server1, server2);
            }

            if (cmd == "import")
            {
                Console.Write("Connection String: ");
                string connString = Console.ReadLine();
                Console.WriteLine();
                Console.Write("Start at: ");
                int startAt = Convert.ToInt32(Console.ReadLine());
                Console.WriteLine();

                _import = new AssetImport(server, connString, startAt);
                _import.Start();
            }

            if (cmd == "msimport")
            {
                Console.Write("Connection String: ");
                string connString = Console.ReadLine();
                Console.WriteLine();
                Console.Write("Start at: ");
                Guid startAt = new Guid(Console.ReadLine());
                Console.WriteLine();

                _msimport = new MsSqlAssetImport(server, connString, startAt);
                _msimport.Start();
            }

            if (cmd == "stop import")
            {
                if (_import != null) _import.Stop();
                _import = null;
                if (_msimport != null) _msimport.Stop();
                _msimport = null;
            }
        }

        private static void RunCompare(RemoteServer server1, RemoteServer server2)
        {
            MD5 md5 = MD5.Create();
            for (int i = 0; i <= 0xFFF; i++)
            {
                string ids = server1.GetAssetIds(i.ToString("X3"));
                string[] splitIds = ids.Split(',');

                foreach (string id in splitIds)
                {
                    if (id == "")
                    {
                        continue;
                    }

                    Asset a = server1.GetAsset(id);
                    Asset b = server2.GetAsset(id);

                    string hashA = GetMd5Hash(md5, a.Data);
                    string hashB = GetMd5Hash(md5, b.Data);

                    if (hashA != hashB)
                    {
                        Console.WriteLine("Fail: {0} != {1}");
                    }
                }
            }
        }

        static string GetMd5Hash(MD5 md5Hash, byte[] input)
        {
            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5Hash.ComputeHash(input);

            // Create a new Stringbuilder to collect the bytes // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }
    }
}
