using Microsoft.WindowsAzure.ServiceRuntime;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Common
{
    public class PipesRuntimeBridge
    {
        //Unique pipe name per instance
        private readonly string _pipename = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
        private readonly string _endpointName;
        public PipesRuntimeBridge(string endpointName)
        {
            var server = new NamedPipeServerStream(_pipename,
                                PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            _endpointName = endpointName;

            BeginWaitForConnection(server);
        }

        public string PipeName { get { return _pipename; } }

        private void BeginWaitForConnection(NamedPipeServerStream listener)
        {
            // Start to listen for connections from a client.
            Trace.TraceInformation("Runtime Bridge Start waiting for connection...");

            listener.BeginWaitForConnection(ConnectionCallback, listener);
        }

        protected void ConnectionCallback(IAsyncResult ar)
        {
            Trace.TraceInformation("Runtime Bridge connection callback");
            // Get the listener that handles the client request.
            NamedPipeServerStream listener = (NamedPipeServerStream)ar.AsyncState;
            // End waiting for the connection
            listener.EndWaitForConnection(ar);

            Trace.TraceInformation("Runtime Bridge connection made");

            var writer = new StreamWriter(listener);

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

            Trace.TraceInformation("Writing node list: {0}",endpointsPayload);

            listener.Disconnect();

            Trace.TraceInformation("Client connected completed starting over");
            BeginWaitForConnection(listener);

        }

    }
}
