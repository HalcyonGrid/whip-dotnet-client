using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using InWorldz.Whip.Client;

namespace whipstress
{
    class CrossServerTestMulticonn
    {
        private const int NUM_THREADS = 30;

        private Dictionary<string, byte[]> _existingAssets = new Dictionary<string, byte[]>();
        private List<string> _assetUuids = new List<string>();
        private List<string> _assetsThatExistOnAll = new List<string>();
        private RemoteServer _server1;
        private RemoteServer _server2;
        private RemoteServer _server3;

        private RemoteServer[] _allConnections;

        private int _asyncReadReturns = 0;
        private int _asyncReadSends = 0;

        Random serverSelect = new Random();
        Random randomAsset = new Random();

        private DateTime[] _threadProgress = new DateTime[NUM_THREADS];

        public CrossServerTestMulticonn(RemoteServer server1, RemoteServer server2, RemoteServer server3,
            RemoteServer[] allConnections, bool putDupesToAll)
        {
            _server1 = server1;
            _server2 = server2;
            _server3 = server3;
            _allConnections = allConnections;

            Console.WriteLine("Putting 100 random assets to server1");
            Console.WriteLine(DateTime.Now);
            SHA1 sha = new SHA1CryptoServiceProvider();


            for (int i = 0; i < 100; i++)
            {
                string uuidstr = OpenMetaverse.UUID.Random().ToString();
                byte[] randomBytes = TestUtil.RandomBytes(500, 800000);
                byte[] challengeHash = sha.ComputeHash(randomBytes);
                _assetUuids.Add(uuidstr);
                _existingAssets.Add(uuidstr, challengeHash);

                Asset asset = new Asset(uuidstr, 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);
                _server1.PutAsset(asset);
            }

            Console.WriteLine("Done: " + DateTime.Now);

            Console.WriteLine("Putting 100 random assets to server2");
            Console.WriteLine(DateTime.Now);


            for (int i = 0; i < 100; i++)
            {
                string uuidstr = OpenMetaverse.UUID.Random().ToString();
                byte[] randomBytes = TestUtil.RandomBytes(500, 800000);
                byte[] challengeHash = sha.ComputeHash(randomBytes);
                _assetUuids.Add(uuidstr);
                _existingAssets.Add(uuidstr, challengeHash);

                Asset asset = new Asset(uuidstr, 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);
                _server2.PutAsset(asset);
            }

            Console.WriteLine("Done: " + DateTime.Now);

            Console.WriteLine("Putting 100 random assets to server3");
            Console.WriteLine(DateTime.Now);


            for (int i = 0; i < 100; i++)
            {
                string uuidstr = OpenMetaverse.UUID.Random().ToString();
                byte[] randomBytes = TestUtil.RandomBytes(500, 800000);
                byte[] challengeHash = sha.ComputeHash(randomBytes);
                _assetUuids.Add(uuidstr);
                _existingAssets.Add(uuidstr, challengeHash);

                Asset asset = new Asset(uuidstr, 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);
                _server3.PutAsset(asset);
            }

            Console.WriteLine("Putting  duplicate assets to all servers");

            for (int i = 0; i < 20; i++)
            {
                string uuidstr = OpenMetaverse.UUID.Random().ToString();
                byte[] randomBytes = TestUtil.RandomBytes(500, 800000);
                byte[] challengeHash = sha.ComputeHash(randomBytes);
                _assetUuids.Add(uuidstr);
                _existingAssets.Add(uuidstr, challengeHash);

                Asset asset = new Asset(uuidstr, 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);
                _server1.PutAsset(asset);

                if (putDupesToAll)
                {
                    _server2.PutAsset(asset);
                    _server3.PutAsset(asset);
                }

                _assetsThatExistOnAll.Add(uuidstr);
            }

            Console.WriteLine("Done: " + DateTime.Now);
        }

        public void Start()
        {
            Console.WriteLine("Starting 30 test threads");
            Thread t;

            for (int i = 0; i < NUM_THREADS; i++)
            {
                t = new Thread(new ParameterizedThreadStart(ThreadProc));
                _threadProgress[i] = DateTime.Now;
                t.Start(i);
            }

            t = new Thread(new ParameterizedThreadStart(MonitorProc));
            t.Start();
        }

        private void MonitorProc(Object obj)
        {
            while (true)
            {
                lock (_threadProgress)
                {
                    for (int i = 0; i < _threadProgress.Length; i++)
                    {
                        DateTime update = _threadProgress[i];

                        if (DateTime.Now - update > TimeSpan.FromSeconds(60))
                        {
                            Console.WriteLine("Thread {0} has stopped making progress", i);
                        }
                    }
                }

                Thread.Sleep(5000);
            }
        }

        private RemoteServer RandomServer()
        {
            lock (serverSelect)
            {
                int which = serverSelect.Next(_allConnections.Length);

                return _allConnections[which];
            }
        }

        public void SingleWrite()
        {
            string uuidstr = OpenMetaverse.UUID.Random().ToString();
            byte[] randomBytes = TestUtil.RandomBytes(500, 800000);

            Asset asset = new Asset(uuidstr, 1,
                false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);

            
            RandomServer().PutAsset(asset);

            //also try to put an asset that already exists
            int index;
            lock (randomAsset)
            {
                index = (int)Math.Floor(_assetsThatExistOnAll.Count * randomAsset.NextDouble());
            }

            try
            {
                Asset existing = new Asset(_assetsThatExistOnAll[index], 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);
                RandomServer().PutAsset(existing);

                Console.WriteLine("Write duplicate expected to error, but no error caught!");
            }
            catch (Exception)
            {
            }
        }

        public void SingleAsyncRead(Random random)
        {
            lock (this)
            {
                _asyncReadSends++;
            }
            if (random.NextDouble() > 0.90)
            {
                //read a non existant asset
                RandomServer().GetAssetAsync("00000000000000000000000000000000",
                    delegate(Asset asset, AssetServerError e)
                    {
                        if (e == null)
                        {
                            Console.WriteLine("Async read expected to error, but no error caught!");

                        }

                        lock (this)
                        {
                            _asyncReadReturns++;
                        }
                    }
                );
            }
            else
            {
                //read an existing asset
                int index = (int)Math.Floor(_assetUuids.Count * random.NextDouble());
                RandomServer().GetAssetAsync(_assetUuids[index],
                    delegate(Asset asset, AssetServerError e)
                    {

                        if (e != null)
                        {
                            Console.WriteLine("Async read expected no error, but error caught! " + e.ToString());
                        }

                        lock (this)
                        {
                            _asyncReadReturns++;
                        }
                    }
                );
            }


        }

        public void ThreadProc(Object obj)
        {
            int threadIdx = (int)obj;
            Random random = new Random();

            //run 20,000 iterations of reads and writes
            for (int i = 0; i < 20000; i++)
            {
                lock (_threadProgress)
                {
                    if (i % 5000 == 0) Console.WriteLine("Thread " + threadIdx + " is making progress " + i);
                    _threadProgress[threadIdx] = DateTime.Now;
                }

                if (random.NextDouble() > 0.95)
                {
                    this.SingleWrite();
                    this.SingleAsyncRead(random);
                }
                else
                {
                    SHA1 sha = new SHA1CryptoServiceProvider();

                    //read an existing asset and check the data hash
                    int index = (int)Math.Floor(_assetUuids.Count * random.NextDouble());

                    try
                    {
                        Asset a = RandomServer().GetAsset(_assetUuids[index]);
                        byte[] hash = sha.ComputeHash(a.Data);

                        if (Util.FixUuid(_assetUuids[index]) != a.Uuid)
                        {
                            Console.WriteLine("Mismatched UUID returned expecting {0} got {1}", _assetUuids[index], a.Uuid);
                        }
                        else if (!TestUtil.Test.test(hash, _existingAssets[_assetUuids[index]]))
                        {
                            Console.WriteLine("Mismatched hash on " + _assetUuids[index]);
                            Console.WriteLine("Got " + Util.HashToHex(hash) + " expected " + Util.HashToHex(_existingAssets[_assetUuids[index]]));
                        }
                    }
                    catch (AssetServerError e)
                    {
                        Console.WriteLine("Error fetching asset {0}: {1}", _assetUuids[index], e);
                    }
                }
            }

            Console.WriteLine("Thread " + threadIdx + " has finished");
        }
    }
}
