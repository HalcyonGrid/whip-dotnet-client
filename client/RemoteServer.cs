using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace InWorldz.Whip.Client
{
    public delegate void AsyncAssetCallback(Asset asset, AssetServerError error);

    /// <summary>
    /// Interface to a remote WHIP asset server.  All public instance methods on this class are
    /// threadsafe
    /// </summary>
    public class RemoteServer
    {
        /// <summary>
        /// Time to wait between reconnection attempts
        /// </summary>
        private int RECONNECT_WAIT_TIME = 5000;

        /// <summary>
        /// Stores the connection host name
        /// </summary>
        private string _hostName;

        /// <summary>
        /// Stores the connection port
        /// </summary>
        private ushort _port;

        /// <summary>
        /// The password to use for the connection
        /// </summary>
        private string _password;

        /// <summary>
        /// Stores the physical socket used for communication with the asset server
        /// </summary>
        private Socket _conn;

        /// <summary>
        /// Object representing the threads that are waiting on a specific get response
        /// </summary>
        private class ResponseWaiter
        {
            /// <summary>
            /// The event the thread is waiting on
            /// </summary>
            public ManualResetEvent waitEvent = new ManualResetEvent(false);

            /// <summary>
            /// A callback to be fired instead of using the waitevent.  Will ONLY be used for get waiters
            /// </summary>
            public AsyncAssetCallback callBack;
            
            /// <summary>
            /// The type of reqest this was
            /// </summary>
            public ClientRequestMsg.RequestType type;

            /// <summary>
            /// The response returned by the server
            /// </summary>
            public ServerResponseMsg response = null;

            /// <summary>
            /// The asset returned if this is a successful get.  This allows
            /// us to share the asset between callers requesting the same asset
            /// </summary>
            public Asset asset = null;

            /// <summary>
            /// Any exceptions thrown while receiving
            /// </summary>
            public Exception error = null;
        }

        /// <summary>
        /// List of events to be signaled when a get request completes
        /// </summary>
        private Dictionary<string, List<ResponseWaiter>> _getResponseWaiters = new Dictionary<string, List<ResponseWaiter>>();

        /// <summary>
        /// Synchronizes send requests
        /// </summary>
        private object _sendSync = new object();

        /// <summary>
        /// A list of the waiting requests to match up their responses 
        /// </summary>
        private Queue<ResponseWaiter> _waitingRequests = new Queue<ResponseWaiter>();

        /// <summary>
        /// The thread currently running the recieve proc
        /// </summary>
        private Thread _receiveThread;

        /// <summary>
        /// Is the server stopping?
        /// </summary>
        private bool _stopping = false;


        public string HostName
        {
            get
            {
                return _hostName;
            }
        }


        /// <summary>
        /// Creates a new instance of an asset server remote connection
        /// </summary>
        /// <param name="hostName">The host to connect to</param>
        /// <param name="port">The port number to connect to</param>
        /// <param name="password">The password to use for the connection</param>
        public RemoteServer(string hostName, ushort port, string password)
        {
            _hostName = hostName;
            _port = port;
            _password = password;
        }

        /// <summary>
        /// Starts the receive process
        /// </summary>
        public void Start()
        {
            _stopping = false;

            this.Connect();
            _receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            _receiveThread.Start();
        }

        /// <summary>
        /// Stops the receive process
        /// </summary>
        public void Stop()
        {
            if (!_stopping)
            {
                _stopping = true;
                this.Disconnect();
                _receiveThread.Join();

                _receiveThread = null;
            }
        }

        private void Disconnect()
        {
            _conn.Shutdown(SocketShutdown.Both);
            _conn.Close();
        }

        /// <summary>
        /// Calls all asynchronus callbacks for GET waiters
        /// </summary>
        /// <param name="uuid">The asset UUID as a string</param>
        /// <returns>Asset</returns>
        private void HandleAsyncResponse(ResponseWaiter waiter)
        {
            if (waiter.callBack != null)
            {
                if (waiter.response.Status == ServerResponseMsg.Result.FOUND)
                {
                    waiter.callBack(waiter.asset, null);
                }
                else
                {
                    waiter.callBack(null, this.DetermineErrorFromStatus(waiter.response));
                }
            }
        }

        /// <summary>
        /// Calls all waiters waiting on the given asset UUID
        /// </summary>
        /// <param name="message"></param>
        private void CallGetWaiters(ServerResponseMsg message, Asset asset)
        {
            List<ResponseWaiter> getWaitList = _getResponseWaiters[message.AssetUUID];
            foreach (ResponseWaiter waiter in getWaitList)
            {
                waiter.response = message;
                waiter.asset = asset;
                waiter.waitEvent.Set();

                this.HandleAsyncResponse(waiter);
            }

            //remove the list, everyone has been notified
            _getResponseWaiters.Remove(message.AssetUUID);
        }

        private Asset TryConstructAsset(ServerResponseMsg message, ResponseWaiter topWaiter)
        {
            if (topWaiter.type == ClientRequestMsg.RequestType.GET)
            {
                if (message.Status == ServerResponseMsg.Result.FOUND)
                {
                    return new Asset(message.Data);
                }
            }

            return null;
        }

        /// <summary>
        /// Loop that receives traffic on the wire and dispatches results
        /// </summary>
        private void ReceiveLoop()
        {
            //wait for packets on the wire
            while (! _stopping)
            {
                try
                {
                    ServerResponseMsg message = new ServerResponseMsg(_conn);

                    //we should have a fully formed message
                    //pop the oldest request

                    ResponseWaiter topWaiter;
                    lock (_waitingRequests)
                    {
                        topWaiter = _waitingRequests.Dequeue();
                    }

                    //test if this is a successful GET and construct an asset
                    Asset asset = this.TryConstructAsset(message, topWaiter);

                    //notify the root waiter
                    topWaiter.response = message;
                    topWaiter.asset = asset;
                    topWaiter.waitEvent.Set();

                    //if this is a get request, also notify all the waiters and pull the entry
                    if (topWaiter.type == ClientRequestMsg.RequestType.GET)
                    {
                        lock (_getResponseWaiters)
                        {
                            if (_getResponseWaiters.ContainsKey(message.AssetUUID))
                            {
                                this.CallGetWaiters(message, asset);
                            }
                        }
                    }
                }
                catch (SocketException e)
                {
                    this.HandleReceiveError(e);
                }
                catch (AssetServerError e)
                {
                    this.HandleReceiveError(e);
                }
                catch (AssetProtocolError e)
                {
                    _conn.Disconnect(false);
                    this.HandleReceiveError(e);
                }
            } //end while
        }

        private void HandleReceiveError(Exception e)
        {
            //connection problem
            this.FreeAllWaitersWithError(e);

            while (! _stopping && !_conn.Connected)
            {
                try
                {
                    this.Connect();
                }
                catch (SocketException)
                {
                    //just keep retrying
                    Thread.Sleep(RECONNECT_WAIT_TIME);
                }
            }
        }

        /// <summary>
        /// Attempts to establish the connection with the remote server
        /// </summary>
        private void Connect()
        {
            const int BUF_SZ = 65536;
            _conn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _conn.ReceiveBufferSize = BUF_SZ;
            _conn.SendBufferSize = BUF_SZ;

            //establish a socket connection
            _conn.Connect(_hostName, _port);

            //read the auth challenge packet to be sent by the server
            AuthChallenge challenge = new AuthChallenge(_conn);

            //construct and send a response
            AuthResponse response = new AuthResponse(challenge, _password);
            response.Send(_conn);

            //check our status
            AuthStatus status = new AuthStatus(_conn);
            if (status.Status != AuthStatus.StatusType.AS_SUCCESS)
            {
                throw new AuthException("Authentication failed, bad password");
            }
        }

        /// <summary>
        /// Queues a wait for a get message.  Allows us to send only one request and notify
        /// multiple threads of a GET response
        /// </summary>
        /// <returns>True there was already a get request out and there is no need to send another, false if not</returns>
        private bool QueueGetWaiter(ClientRequestMsg request, ResponseWaiter waiter)
        {
            bool requestExisted = false;
            if (request.Type == ClientRequestMsg.RequestType.GET)
            {
                lock (_getResponseWaiters)
                {
                    string uuid = request.GetUuid();

                    //if someone is already waiting for a response, 
                    //make a note, we dont have to send out a request
                    if (_getResponseWaiters.ContainsKey(uuid))
                    {
                        requestExisted = true;
                    }
                    else
                    {
                        _getResponseWaiters.Add(request.GetUuid(), new List<ResponseWaiter>());
                    }

                    //insert us as a waiter
                    _getResponseWaiters[uuid].Add(waiter);
                }
            }

            return requestExisted;
        }


        /// <summary>
        /// Sends the given request to the server
        /// </summary>
        /// <param name="request">The request to send</param>
        private ResponseWaiter SendRequest(ClientRequestMsg request, AsyncAssetCallback callBack)
        {
            ResponseWaiter waiter = new ResponseWaiter();
            waiter.callBack = callBack;
            waiter.type = request.Type;

            if (!this.QueueGetWaiter(request, waiter))
            {
                lock (_sendSync)
                {
                    //we need to enqueue the right waiter here in the right order
                    lock (_waitingRequests)
                    {
                        _waitingRequests.Enqueue(waiter);
                    }

                    request.Send(_conn);
                }
            }

            return waiter;
        }

        private AssetServerError DetermineErrorFromStatus(ServerResponseMsg response)
        {
            if (response.Status == ServerResponseMsg.Result.ERROR)
            {
                return new AssetServerError(response.ErrorMessage);
            }

            if (response.Status == ServerResponseMsg.Result.NOT_FOUND)
            {
                return new AssetServerError("Asset " + response.AssetUUID + " was not found");
            }

            return null;
        }

        /// <summary>
        /// Checks the server response for errors and throws an exception if 
        /// there is a problem
        /// </summary>
        /// <param name="serverResponseMsg">The response from the server</param>
        private void CheckThrowError(ResponseWaiter responseWaiter)
        {
            if (responseWaiter.error != null)
            {
                throw new AssetServerError(responseWaiter.error.Message, responseWaiter.error);
            }

            AssetServerError err = DetermineErrorFromStatus(responseWaiter.response);
            if (err != null)
            {
                throw err;
            }
        }

        /// <summary>
        /// Attempts to retrieve the given asset from the server synchronously
        /// </summary>
        /// <param name="uuid">The asset UUID as a string</param>
        /// <returns>Asset</returns>
        public void GetAssetAsync(string uuid, AsyncAssetCallback callBack)
        {
            uuid = Util.FixUuid(uuid);

            //build the request
            ClientRequestMsg request = new ClientRequestMsg(ClientRequestMsg.RequestType.GET, uuid);

            //send request and wait for response
            try
            {
                this.SendRequest(request, callBack);
            }
            catch (SocketException e)
            {
                //a socket exception means we need to signal all waiters
                this.HandleSendError(e);
                throw new AssetServerError(e.Message, e);
            }
        }

        /// <summary>
        /// Attempts to retrieve the given asset from the server synchronously
        /// </summary>
        /// <param name="uuid">The asset UUID as a string</param>
        /// <returns>Asset</returns>
        public Asset GetAsset(string uuid)
        {
            uuid = Util.FixUuid(uuid);

            //build the request
            ClientRequestMsg request = new ClientRequestMsg(ClientRequestMsg.RequestType.GET, uuid);
            
            //send request and wait for response
            try
            {
                ResponseWaiter responseWaiter = this.SendRequest(request, null);
                responseWaiter.waitEvent.WaitOne();

                //we got a response 
                //is there an error?
                this.CheckThrowError(responseWaiter);

                //no error, return the asset
                return responseWaiter.asset;
            }
            catch (SocketException e)
            {
                //a socket exception means we need to signal all waiters
                this.HandleSendError(e);
                throw new AssetServerError(e.Message, e);
            }
        }

        private const string ZERO_UUID = "00000000-0000-0000-0000-000000000000";

        /// <summary>
        /// Attempts to retrieve the given asset from the server synchronously
        /// </summary>
        /// <param name="uuid">The asset UUID as a string</param>
        /// <returns>Asset</returns>
        public void MaintPurgeLocals()
        {
            string uuid = ZERO_UUID;

            //build the request
            ClientRequestMsg request = new ClientRequestMsg(ClientRequestMsg.RequestType.MAINT_PURGELOCALS, uuid);

            //send request and wait for response
            try
            {
                ResponseWaiter responseWaiter = this.SendRequest(request, null);
                responseWaiter.waitEvent.WaitOne();

                //we got a response 
                //is there an error?
                this.CheckThrowError(responseWaiter);
            }
            catch (SocketException e)
            {
                //a socket exception means we need to signal all waiters
                this.HandleSendError(e);
                throw new AssetServerError(e.Message, e);
            }
        }

        public string GetServerStatus()
        {
            string uuid = ZERO_UUID;

            //build the request
            ClientRequestMsg request = new ClientRequestMsg(ClientRequestMsg.RequestType.STATUS_GET, uuid);

            //send request and wait for response
            try
            {
                ResponseWaiter responseWaiter = this.SendRequest(request, null);
                responseWaiter.waitEvent.WaitOne();

                //we got a response 
                //is there an error?
                this.CheckThrowError(responseWaiter);

                //ErrorMessage takes the status string out of the data field
                return responseWaiter.response.ErrorMessage;
            }
            catch (SocketException e)
            {
                //a socket exception means we need to signal all waiters
                this.HandleSendError(e);
                throw new AssetServerError(e.Message, e);
            }
        }

        public string GetAssetIds(string prefix)
        {
            //build the request
            ClientRequestMsg request = new ClientRequestMsg(ClientRequestMsg.RequestType.STORED_ASSET_IDS_GET, prefix);

            //send request and wait for response
            try
            {
                ResponseWaiter responseWaiter = this.SendRequest(request, null);
                responseWaiter.waitEvent.WaitOne();

                //we got a response 
                //is there an error?
                this.CheckThrowError(responseWaiter);

                //ErrorMessage takes the status string out of the data field
                return responseWaiter.response.ErrorMessage;
            }
            catch (SocketException e)
            {
                //a socket exception means we need to signal all waiters
                this.HandleSendError(e);
                throw new AssetServerError(e.Message, e);
            }
        }

        /// <summary>
        /// Attempts to retrieve the given asset from the server
        /// </summary>
        /// <param name="uuid">The asset UUID as a string</param>
        /// <returns>Asset</returns>
        public void PutAsset(Asset asset)
        {

            //build the request
            ClientRequestMsg request = new ClientRequestMsg(ClientRequestMsg.RequestType.PUT, asset.Uuid, asset.Serialize().data);

            //send request and wait for response
            try
            {
                ResponseWaiter responseWaiter = this.SendRequest(request, null);
                responseWaiter.waitEvent.WaitOne();

                //we got a response 
                //is there an error?
                this.CheckThrowError(responseWaiter);
            }
            catch (SocketException e)
            {
                //a socket exception means we need to signal all waiters
                this.HandleSendError(e);
                throw new AssetServerError(e.Message, e);
            }
        }

        private void HandleSendError(SocketException e)
        {
            //free all waiters and inform of the exception,
            //receiving thread will preform reconnect
            this.FreeAllWaitersWithError(e);
        }

        private void FreeAllWaitersWithError(Exception e)
        {

            lock (_waitingRequests)
            {
                foreach (ResponseWaiter waiter in _waitingRequests)
                {
                    waiter.error = e;
                    waiter.waitEvent.Set();
                }

                _waitingRequests.Clear();
            }

            lock (_getResponseWaiters)
            {
                AssetServerError ase = new AssetServerError(e.Message, e);

                foreach (KeyValuePair<string, List<ResponseWaiter>> waiterPair in _getResponseWaiters)
                {
                    foreach (ResponseWaiter waiter in waiterPair.Value)
                    {
                        waiter.error = e;
                        waiter.waitEvent.Set();

                        if (waiter.callBack != null)
                        {
                            //also call async
                            waiter.callBack(null, ase);
                        }
                    }

                }

                _getResponseWaiters.Clear();
            }
        }
    }
}
