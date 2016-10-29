using ElasticsearchWorker.Core;
using Microsoft.WindowsAzure.ServiceRuntime;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ElasticsearchWorker.Discovery
{
    public class TcpRuntimeBridge
    {
        //Unique pipe name per instance
       
        private readonly string _endpointName;
        private readonly TcpListener _server;
        public TcpRuntimeBridge(string endpointName)
        {
            _endpointName = endpointName;
            _server = new TcpListener(IPAddress.Loopback, 0);
            _server.Start();

        }

        public void StartService()
        {
            BeginWaitForConnection(_server);
           
        }

        public int Port { get { return ((IPEndPoint)_server.LocalEndpoint).Port; } }

        private void BeginWaitForConnection(TcpListener listener)
        {
            // Start to listen for connections from a client.
            Trace.TraceInformation("Runtime Bridge Start waiting for connection...");
            listener.BeginAcceptTcpClient(new AsyncCallback(ConnectionCallback),listener);
            
        }

        protected void ConnectionCallback(IAsyncResult ar)
        {
            Trace.TraceInformation("Runtime Bridge connection callback");
            // Get the listener that handles the client request.
            TcpListener listener = (TcpListener)ar.AsyncState;
            // End waiting for the connection
            TcpClient client = listener.EndAcceptTcpClient(ar);

            Trace.TraceInformation("Runtime Bridge connection made");

            var stream = client.GetStream();

            using (var writer = new StreamWriter(stream))
            using (var reader = new StreamReader(stream))
            {
                writer.AutoFlush = true;

                var endpoints = from r in RoleEnvironment.Roles
                                from i in r.Value.Instances
                                from e in i.InstanceEndpoints
                                where e.Key == _endpointName
                                select new ElasticsearchNode
                                {
                                    Ip = e.Value.IPEndpoint.Address.ToString(),
                                    Port = e.Value.IPEndpoint.Port,
                                    NodeName = i.Id
                                };

                var endpointsPayload = JsonConvert.SerializeObject(endpoints);

                writer.WriteLine(endpointsPayload);

                Trace.TraceInformation("Writing node list: {0}", endpointsPayload);
            }
            // Process the connection here. (Add the client to a 
            // server table, read data, etc.)
            
            Trace.TraceInformation("Node list sent! Waiting for new request.");
            BeginWaitForConnection(listener);

        }

    }
}
