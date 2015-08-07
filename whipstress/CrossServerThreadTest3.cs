using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using InWorldz.Whip.Client;

namespace whipstress
{
    class CrossServerThreadTest3
    {
        private Dictionary<string, byte[]> _existingAssets = new Dictionary<string, byte[]>();
        private List<string> _assetUuids = new List<string>();
        private RemoteServer _server1;
        private RemoteServer _server2;
        private RemoteServer _server3;

        private int _asyncReadReturns = 0;
        private int _asyncReadSends = 0;

        Random serverSelect = new Random();

        public CrossServerThreadTest3(RemoteServer server1, RemoteServer server2, RemoteServer server3)
        {
            _server1 = server1;
            _server2 = server2;
            _server3 = server3;

            //setup the test by adding 200 shared assets
            Console.WriteLine("Putting 100 random assets to server1");
            Console.WriteLine(DateTime.Now);
            SHA1 sha = new SHA1CryptoServiceProvider();


            for (int i = 0; i < 100; i++)
            {
                string uuidstr = OpenMetaverse.UUID.Random().ToString();
                byte[] randomBytes = TestUtil.RandomBytes();
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
                byte[] randomBytes = TestUtil.RandomBytes();
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
                byte[] randomBytes = TestUtil.RandomBytes();
                byte[] challengeHash = sha.ComputeHash(randomBytes);
                _assetUuids.Add(uuidstr);
                _existingAssets.Add(uuidstr, challengeHash);

                Asset asset = new Asset(uuidstr, 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);
                _server3.PutAsset(asset);
            }

            Console.WriteLine("Done: " + DateTime.Now);
        }

        public void Start()
        {
            Console.WriteLine("Starting 30 test threads");
            Thread t;

            for (int i = 0; i < 30; i++)
            {
                t = new Thread(new ParameterizedThreadStart(ThreadProc));
                t.Start(i);
            }
        }

        private RemoteServer RandomServer()
        {
            lock (serverSelect)
            {
                int which = serverSelect.Next(3);

                if (which == 0)
                {
                    return _server1;
                }
                else if (which == 1)
                {
                    return _server2;
                }
                else
                {
                    return _server3;
                }
            }
        }

        public void SingleWrite()
        {
            string uuidstr = OpenMetaverse.UUID.Random().ToString();
            byte[] randomBytes = TestUtil.RandomBytes();

            Asset asset = new Asset(uuidstr, 1,
                false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);

            
            RandomServer().PutAsset(asset);
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
                            Console.WriteLine("async:  sent: " + _asyncReadSends + " rcvd: " + _asyncReadReturns);
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
                            Console.WriteLine("async:  sent: " + _asyncReadSends + " rcvd: " + _asyncReadReturns);
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
                if (i % 100 == 0) Console.WriteLine("Thread " + threadIdx + " is making progress " + i);

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

                    Asset a = RandomServer().GetAsset(_assetUuids[index]);
                    byte[] hash = sha.ComputeHash(a.Data);
                    if (!TestUtil.Test.test(hash, _existingAssets[_assetUuids[index]]))
                    {
                        Console.WriteLine("Mismatched hash on " + _assetUuids[index]);
                        Console.WriteLine("Got " + Util.HashToHex(hash) + " expected " + Util.HashToHex(_existingAssets[_assetUuids[index]]));

                        ASCIIEncoding encoding = new ASCIIEncoding();

                        Console.WriteLine("Data " + encoding.GetString(a.Data));
                    }
                }
            }

            Console.WriteLine("Thread " + threadIdx + " has finished");
        }
    }
}
