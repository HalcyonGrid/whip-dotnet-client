using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using InWorldz.Whip.Client;

namespace whipstress
{
    class CrossServerBiasTest3
    {
        private Dictionary<string, byte[]> _existingAssets = new Dictionary<string, byte[]>();
        private List<string> _assetUuids = new List<string>();
        private RemoteServer _server1;
        private RemoteServer[] _server2;
        private RemoteServer _server3;

        private int _asyncReadReturns = 0;
        private int _asyncReadSends = 0;

        Random serverSelect = new Random();

        public CrossServerBiasTest3(RemoteServer server1, RemoteServer[] server2, RemoteServer server3)
        {
            _server1 = server1;
            _server2 = server2;
            _server3 = server3;

            //setup the test by adding 200 shared assets
            Console.WriteLine("Putting 200 random assets to server1");
            Console.WriteLine(DateTime.Now);
            SHA1 sha = new SHA1CryptoServiceProvider();


            for (int i = 0; i < 200; i++)
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

            Console.WriteLine("Putting 10 random assets to server2");
            Console.WriteLine(DateTime.Now);


            for (int i = 0; i < 10; i++)
            {
                string uuidstr = OpenMetaverse.UUID.Random().ToString();
                byte[] randomBytes = TestUtil.RandomBytes(1000, 50000);
                byte[] challengeHash = sha.ComputeHash(randomBytes);
                _assetUuids.Add(uuidstr);
                _existingAssets.Add(uuidstr, challengeHash);

                Asset asset = new Asset(uuidstr, 1,
                    false, false, 0, "Random Asset", "Radom Asset Desc", randomBytes);
                _server2[0].PutAsset(asset);
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
                int which = serverSelect.Next(11);

                if (which == 0 || which < 10)
                {
                    return _server2[which];
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

            lock (_existingAssets)
            {
                SHA1 sha = new SHA1CryptoServiceProvider();
                byte[] challengeHash = sha.ComputeHash(randomBytes);
                _assetUuids.Add(uuidstr);
                _existingAssets.Add(uuidstr, challengeHash);
            }
        }

        public void SingleAsyncRead(Random random)
        {
            lock (this)
            {
                _asyncReadSends++;
            }
            if (random.NextDouble() > 0.5)
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
                string reqUuid;
                lock (_existingAssets)
                {
                    //read an existing asset
                    int index = (int)Math.Floor(_assetUuids.Count * random.NextDouble());
                    reqUuid = _assetUuids[index];
                }

                RandomServer().GetAssetAsync(reqUuid,
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

            //run 200,000 iterations of reads and writes
            for (int i = 0; i < 200000; i++)
            {
                if (i % 100 == 0) Console.WriteLine("Thread " + threadIdx + " is making progress " + i);

                if (random.NextDouble() > 0.9999)
                {
                    this.SingleWrite();
                    this.SingleAsyncRead(random);
                }
                else
                {
                    SHA1 sha = new SHA1CryptoServiceProvider();

                    string reqUuid;
                    byte[] existingHash;
                    lock (_existingAssets)
                    {
                        //read an existing asset
                        int index = (int)Math.Floor(_assetUuids.Count * random.NextDouble());
                        reqUuid = _assetUuids[index];
                        existingHash = _existingAssets[reqUuid];
                    }

                    Asset a = RandomServer().GetAsset(reqUuid);

                    //only test 5mb or less.  this is to try and trigger specific code in the server
                    if (a.Data.Length < 5000000)
                    {
                        byte[] hash = sha.ComputeHash(a.Data);
                        if (!TestUtil.Test.test(hash, existingHash))
                        {
                            Console.WriteLine("Mismatched hash on " + reqUuid);
                            Console.WriteLine("Got " + Util.HashToHex(hash) + " expected " + Util.HashToHex(existingHash));

                            ASCIIEncoding encoding = new ASCIIEncoding();

                            Console.WriteLine("Data " + encoding.GetString(a.Data));
                        }
                    }
                }
            }

            Console.WriteLine("Thread " + threadIdx + " has finished");
        }
    }
}
