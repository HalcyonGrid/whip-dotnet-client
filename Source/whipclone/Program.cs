using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InWorldz.Whip.Client;
using System.IO;
using System.Data.SQLite;
using System.Threading;

namespace whipclone
{
    class Program
    {
        class Work
        {
            private string _UUID;
            private AssetCopyProc _proc;

            public Work(string uuid, AssetCopyProc proc)
            {
                _UUID = uuid;
                _proc = proc;
            }

            public void DoWork()
            {
                _proc(_UUID);
            }
        }

        static Int64 NumCopied;
        static SQLiteConnection Conn;
        static Queue<Work> WorkQueue = new Queue<Work>();
        static short MAX_QUEUE = 3;
        static bool Stopping;

        delegate void AssetCopyProc(string uuid);

        static RemoteServer ConnectServerByConsole()
        {
            Console.Write("Host: ");
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

        static string GetTransferProgressFileName(string srcHost, string dstHost)
        {
            return srcHost + "_" + dstHost + ".progress.s3db";
        }

        static bool TransferProgressFileExists(string srcHost, string dstHost)
        {
            if (File.Exists(GetTransferProgressFileName(srcHost, dstHost)))
            {
                return true;
            }

            return false;
        }

        static void CopyAsset(string uuid, RemoteServer src, RemoteServer dst)
        {
            lock (Conn)
            {
                using (SQLiteCommand countCmd = new SQLiteCommand("SELECT COUNT(*) FROM copied_assets WHERE uuid = '" + uuid + "'", Conn))
                {
                    int count = Convert.ToInt32(countCmd.ExecuteScalar());

                    if (count != 0)
                    {
                        Console.WriteLine("Skipping {0}", uuid);
                        return;
                    }
                }
            }

            Console.WriteLine("Copying {0} ({1})", uuid, NumCopied);

            try
            {
                Asset asset = src.GetAsset(uuid);
                dst.PutAsset(asset);

                lock (Conn)
                {
                    using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO copied_assets(uuid) VALUES('" + uuid + "');", Conn))
                    {
                        cmd.ExecuteNonQuery();

                        NumCopied++;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to copy asset: " + e.Message);
                using (StreamWriter writer = new StreamWriter("error.txt"))
                {
                    writer.WriteLine("Unable to copy asset: " + e.Message);
                    writer.Close();
                }

                Console.WriteLine("Error detected, any key continues");
                Console.ReadLine();
            }

            
        }

        static void FindAssetsRecursive(string parentDir, AssetCopyProc callBack)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(parentDir);
            foreach (FileInfo file in dirInfo.GetFiles("*.asset"))
            {
                callBack(Path.GetFileNameWithoutExtension(file.Name));
            }

            foreach (DirectoryInfo dir in dirInfo.GetDirectories())
            {
                FindAssetsRecursive(dir.FullName, callBack);
            }
        }

        static void FindAssetsCounter(int start, string parentDir, AssetCopyProc callBack)
        {
            for (int i = start; i < 0xfff; i++)
            {
                string subDir = Path.Combine(parentDir, String.Format("{0:x2}", i));
                FindAssetsRecursive(subDir, callBack);
            }
        }

        static void QueueWork(string uuid, AssetCopyProc callBack)
        {
            lock (WorkQueue)
            {
                if (WorkQueue.Count == MAX_QUEUE)
                {
                    Monitor.Wait(WorkQueue);
                }

                WorkQueue.Enqueue(new Work(uuid, callBack));
            }
        }

        static void ThreadProc()
        {
            bool doWait = false;

            while (!Stopping)
            {
                lock (WorkQueue)
                {
                    if (WorkQueue.Count == 0)
                    {
                        doWait = true;
                        goto wait;
                    }

                    Work work = WorkQueue.Dequeue();
                    work.DoWork();
                    Monitor.Pulse(WorkQueue);
                }

            wait:
                if (doWait)
                {
                    doWait = false;
                    Thread.Sleep(50);
                }
            }
        }

        static void Main(string[] args)
        {
        startOver:
            Console.WriteLine("Asset storage directory: ");
            string storageDir = Console.ReadLine();

            Console.Write("Start at: ");
            string startAt = Console.ReadLine();
            int startAtI = 0;

            if (startAt != "")
            {
                startAtI = Convert.ToInt32(startAt, 16);
            }

            if (!Directory.Exists(storageDir))
            {
                Console.WriteLine("Invalid storage directory");
                goto startOver;
            }

            Console.WriteLine("Source server Information");
            RemoteServer sourceSrv = ConnectServerByConsole();

            Console.WriteLine("Destination server Information");
            RemoteServer destSrv = ConnectServerByConsole();

            //see if we have a file for this transfer in progress
            string progressFile = GetTransferProgressFileName(sourceSrv.HostName, destSrv.HostName);
            if (! TransferProgressFileExists(sourceSrv.HostName, destSrv.HostName))
            {
                //copy the blank and create a new one then open it
                File.Copy("blank.progress.s3db", progressFile);
            }

            //open the transfer database
            using (Conn = new SQLiteConnection("Data Source=" + progressFile))
            {
                Conn.Open();

                SQLiteCommand countCmd = new SQLiteCommand("SELECT COUNT(*) FROM copied_assets", Conn);
                NumCopied = Convert.ToInt64(countCmd.ExecuteScalar());

                Console.Write("Assets completed so far: " + Convert.ToString(NumCopied));

                Thread t1 = new Thread(Program.ThreadProc);
                Thread t2 = new Thread(Program.ThreadProc);
                Thread t3 = new Thread(Program.ThreadProc);
                t1.Start();
                t2.Start();
                t3.Start();

                if (startAt == "")
                {
                    FindAssetsRecursive(storageDir,
                        delegate(string uuid)
                        {
                            QueueWork(uuid, delegate(string muuid)
                            {
                                CopyAsset(muuid, sourceSrv, destSrv);
                            });
                        });
                }
                else
                {
                    FindAssetsCounter(startAtI, storageDir,
                        delegate(string uuid)
                        {
                            QueueWork(uuid, delegate(string muuid)
                            {
                                CopyAsset(muuid, sourceSrv, destSrv);
                            });
                        });
                }
            }

            sourceSrv.Stop();
            destSrv.Stop();

            Console.Write("Copy iteration finished");
            Console.ReadLine();
        }
    }
}
