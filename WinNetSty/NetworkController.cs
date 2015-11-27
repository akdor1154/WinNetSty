using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Windows.Networking.Sockets;
using Windows.Networking;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Windows.Storage.Streams;
using System.Threading;

namespace WinNetSty {

    public enum NetworkErrorEventType {
        NO_HOST,
        NO_STREAM,
        CONNECTED,
        NO_DATA_WRITER
    }

    public class NetworkErrorEventArgs : EventArgs {

        public NetworkErrorEventType Type { get; set; }
        public String Message { get; set; }

        public NetworkErrorEventArgs(NetworkErrorEventType type, String message) {
            this.Type = type;
            this.Message = message; 
        }
    }

    class NetworkException : Exception {
        public NetworkErrorEventType Type { get; set; }
        public NetworkException(NetworkErrorEventType type, String message) : base(message) {
            this.Type = type;
        }
    }

    class NetworkController {
        
        DatagramSocket socket;

        private enum EventType : byte {
            Motion = 0x00,
            Button = 0x01
        }

        
        private class GfxTabletPacket {
            public readonly byte[] header = { (byte)'G', (byte)'f', (byte)'x', (byte)'T', (byte)'a', (byte)'b', (byte)'l', (byte)'e', (byte)'t' };
            public readonly ushort version = 0x02;
            public EventType type;
            public ushort x;
            public ushort y;
            public ushort pressure;

            public GfxTabletPacket(InkEventArgs args) {
                this.type = EventType.Motion;
                this.x = (ushort) (args.Position.X * ushort.MaxValue);
                this.y = (ushort) (args.Position.Y * ushort.MaxValue);
                this.pressure = (args.Pressure.HasValue)
                    ? (ushort) (args.Pressure.Value * short.MaxValue)
                    : (ushort) short.MaxValue;
            }

            public virtual byte[] ToBytes() {
                byte[] data = new byte[18];

                Array.Copy(header, 0, data, 0, 9);
                data.setFromUshort(9, version);
                data[11] = (byte) type;
                data.setFromUshort(12, x);
                data.setFromUshort(14, y);
                data.setFromUshort(16, pressure);

                return data;
            }
        }
        
        private class GfxTabletButtonPacket : GfxTabletPacket {
            public ButtonType button;
            public ButtonStatus status;

            public GfxTabletButtonPacket(InkButtonEventArgs args) : base(args) {
                this.type = EventType.Button;
                this.button = args.Button;
                this.status = args.ButtonStatus;
            }

            public override byte[] ToBytes() {
                byte[] data = new byte[20];

                byte[] baseData = base.ToBytes();
                Array.Copy(baseData, data, baseData.Length);

                data[18] = (byte) this.button;
                data[19] = (byte) this.status;

                return data;
            }
        }

        public delegate void NetworkErrorEventHandler(NetworkController sender, NetworkErrorEventArgs e);
        public event NetworkErrorEventHandler NetworkError;

        private Settings settings;

        private String TargetAddress => this.settings.RemoteHost;
        private UInt16 Port => this.settings.RemotePort.Value;

        private DataWriter outputWriter;

        public NetworkController() {
            this.settings = ((WinNetStyApp)WinNetStyApp.Current).Settings;

            this.EstablishConnection();
            this.settings.NetworkChanged += OnNetworkChanged;
        }


        private void RaiseError(NetworkErrorEventType type, String message) {
            Debug.WriteLine(message);
            NetworkError?.Invoke(this, new NetworkErrorEventArgs(type, message));
        }

        private void OnNetworkChanged(object sender, EventArgs e) {
            if (this.connectionAttempt.Status < TaskStatus.RanToCompletion) {
                this.connectionAttempt.AsAsyncAction().Cancel();
                this.EstablishConnection();
            }
            this.EstablishConnection();
        }

        private ConnectionState connectionStatus = ConnectionState.DISCONNECTED;
        private Task connectionAttempt;
        private enum ConnectionState {
            DISCONNECTED,
            CONNECTING,
            CONNECTED,
        }

        private async Task EnsureConnection() {
            switch (this.connectionStatus) {
                case ConnectionState.DISCONNECTED:
                    await this.EstablishConnection();
                    break;
                case ConnectionState.CONNECTING:
                    if (this.connectionAttempt != null) {
                        await this.connectionAttempt;
                    }
                    break;
                case ConnectionState.CONNECTED:
                    break;
            }
        }

        private async Task EstablishConnection() {
            Task _connectionAttempt = Task.Run(() => this.EstablishConnectionAsync());
            TaskCompletionSource<Boolean> tcs = new TaskCompletionSource<bool>();
            this.connectionAttempt = tcs.Task;
            try {
                await _connectionAttempt;
                RaiseError(NetworkErrorEventType.CONNECTED, String.Format("Spraying touch events to the requested destination!"));
            } catch (NetworkException e) {
                this.connectionStatus = ConnectionState.DISCONNECTED;
                RaiseError(e.Type, e.Message);
            } finally {
                tcs.SetResult(true);
            }
            return;
        }

        private async Task EstablishConnectionAsync() {
            connectionStatus = ConnectionState.CONNECTING;
            if (this.socket != null) {
                this.socket.Dispose();
            }

            if (this.outputWriter != null) {
                try {
                    await this.outputWriter.FlushAsync();
                    this.outputWriter.DetachStream();
                } catch (Exception e) {

                } finally {
                    this.outputWriter.Dispose();
                }
            }

            this.socket = new DatagramSocket();

            HostName target;
            IOutputStream outputStream;

            try {
                target = new HostName(TargetAddress);
            }
            catch (Exception e) {
                String message = String.Format("Error getting target host: {0}", e.Message);
                throw new NetworkException(NetworkErrorEventType.NO_HOST, message);
            }


            try {
                outputStream = await socket.GetOutputStreamAsync(target, Port.ToString());
            }
            catch (Exception e) {
                String message = String.Format("Error creating output stream to {1}:{2}: {0}", e.Message, target.RawName, Port);
                throw new NetworkException(NetworkErrorEventType.NO_STREAM, message);
            }

            try {
                this.outputWriter = new DataWriter(outputStream);
            } catch (Exception e) {
                String message = String.Format("Error creating datawriter: {0}", e.Message);
                throw new NetworkException(NetworkErrorEventType.NO_DATA_WRITER, message);

            }
            this.connectionStatus = ConnectionState.CONNECTED;
        }

        public void SendInkButton(InkButtonEventArgs args) {
            Debug.WriteLine("Sending button {0} {1}", args.Button, args.ButtonStatus);
            GfxTabletButtonPacket packet = new GfxTabletButtonPacket(args);
            SendPacket(packet);
        }
        

        public void SendInkMove(InkEventArgs args) {
            GfxTabletPacket packet = new GfxTabletPacket(args);
            SendPacket(packet);
        }

        private async void SendPacket(GfxTabletPacket packet) {
            byte[] packetRaw = packet.ToBytes();

            await this.EnsureConnection();

            if (this.connectionStatus == ConnectionState.CONNECTED) {
                this.outputWriter.WriteBytes(packetRaw);
                await outputWriter.StoreAsync();
            }
        }

    }
}
