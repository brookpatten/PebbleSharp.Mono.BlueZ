﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PebbleSharp.Core
{
    /// <summary>
    ///     Handles the basic protocol structure for Pebble communication.
    ///     Essentially handles the SerialPort and translates the stream to
    ///     endpoint,payload pairs and vv.  Does and should not handle anything
    ///     regarding the *meaning* of that data.
    /// </summary>
    internal class PebbleProtocol
    {
        private readonly IBluetoothConnection _blueToothConnection;
        private readonly Queue<byte> _byteStream = new Queue<byte>();
        private ushort _CurrentEndpoint;
        private ushort _CurrentPayloadSize;
        private WaitingStates _WaitingState;

        /// <summary> Create a new Pebble connection </summary>
        /// <param name="connection"></param>
        public PebbleProtocol( IBluetoothConnection connection )
        {
            _blueToothConnection = connection;

            _blueToothConnection.DataReceived += SerialPortDataReceived;
            //TODO: Push this on to the clients.... do we even care if there is an error?
            //_BlueToothPort.ErrorReceived += serialPortErrorReceived;
        }

        public IBluetoothConnection Connection
        {
            get { return _blueToothConnection; }
        }

        public event EventHandler<RawMessageReceivedEventArgs> RawMessageReceived = delegate { };

        /// <summary> Connect to the Pebble. </summary>
        /// <exception cref="System.IO.IOException">Passed on when no connection can be made.</exception>
        public async Task ConnectAsync()
        {
            await _blueToothConnection.OpenAsync();
        }

        public void Close()
        {
            _blueToothConnection.Close();
        }

        /// <summary>
        ///     Send a message to the connected Pebble.
        ///     The payload should at most be 2048 bytes large.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="payload"></param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the payload is too large.</exception>
        public void SendMessage( ushort endpoint, byte[] payload )
        {
			//System.Console.WriteLine("SEND:" + ((Endpoint)endpoint));
			if ( payload.Length > 2048 )
            {
                throw new ArgumentOutOfRangeException( "payload",
                                                      "The payload cannot not be more than 2048 bytes" );
            }

            UInt16 length = Convert.ToUInt16( payload.Length );
            byte[] payloadSize = Util.GetBytes( length );
            byte[] endPoint = Util.GetBytes( endpoint );

			var packet = Util.CombineArrays(payloadSize, endPoint, payload);
			//LogPacket("SEND",packet);
            _blueToothConnection.Write(packet);
        }

		public void LogPacket(string action,byte[] packet)
		{
			string hex = BitConverter.ToString(packet);
			hex = hex.Replace("-","").ToLower();
			Console.WriteLine(action+":" + hex);
		}

        private void SerialPortDataReceived( object sender, BytesReceivedEventArgs e )
        {
            lock ( _byteStream )
            {
                foreach ( var b in e.Bytes )
                {
                    _byteStream.Enqueue( b );
                }
                // Keep reading until there's no complete chunk to be read.
                while ( ReadAndProcessBytes() )
                { }
            }
        }

        /// <summary>
        ///     Read from the serial line if a useful chunk is present.
        /// </summary>
        /// <remarks>
        ///     In this case a "useful chunk" means that either the payload size
        ///     and endpoint of a new message or the complete payload of a message
        ///     are present.
        /// </remarks>
        /// <returns>
        ///     True if there was enough data to read, otherwise false.
        /// </returns>
        private bool ReadAndProcessBytes()
        {
            switch ( _WaitingState )
            {
                case WaitingStates.NewMessage:
                    if ( _byteStream.Count >= 4 )
                    {
                        // Read new payload size and endpoint
                        _CurrentPayloadSize = Util.GetUInt16( ReadBytes( 2 ) );
                        _CurrentEndpoint = Util.GetUInt16( ReadBytes( 2 ) );

                        _WaitingState = WaitingStates.Payload;
                        return true;
                    }
                    break;
                case WaitingStates.Payload:
                    if ( _byteStream.Count >= _CurrentPayloadSize )
                    {
                        // All of the payload's been received, so read it.
                        var buffer = ReadBytes( _CurrentPayloadSize );

						//byte[] fullPacket = Util.CombineArrays(Util.GetBytes(_CurrentPayloadSize),Util.GetBytes(_CurrentEndpoint),buffer);
						//System.Console.WriteLine("RECV:" + ((Endpoint)_CurrentEndpoint));
						//LogPacket("RECV", fullPacket);

						//Console.WriteLine("RECV:" + ((Endpoint)_CurrentEndpoint).ToString()+"["+buffer.Length+"]");
                        RawMessageReceived( this, new RawMessageReceivedEventArgs( _CurrentEndpoint, buffer ) );
                        // Reset state
                        _WaitingState = WaitingStates.NewMessage;
                        _CurrentEndpoint = 0;
                        _CurrentPayloadSize = 0;
                        return true;
                    }
                    break;
            }
            // If this hasn't returned yet there wasn't anything interesting to read.
            return false;
        }

        private byte[] ReadBytes( int count )
        {
            return Enumerable.Range( 0, count ).Select( x => _byteStream.Dequeue() ).ToArray();
        }

        private enum WaitingStates
        {
            NewMessage,
            Payload
        }
    }
}