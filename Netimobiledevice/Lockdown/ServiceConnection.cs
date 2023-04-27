using Netimobiledevice.EndianBitConversion;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Netimobiledevice.Lockdown
{
    /// <summary>
    /// A wrapper for usbmux tcp-relay connections
    /// </summary>
    public class ServiceConnection
    {
        private UsbmuxdDevice? muxDevice;
        private Stream networkStream;

        private ServiceConnection(Socket sock, UsbmuxdDevice? muxDevice = null)
        {
            networkStream = new NetworkStream(sock);
            // Usbmux connections contain additional information associated with the current connection
            this.muxDevice = muxDevice;
        }

        private static ServiceConnection CreateUsingTcp(string hostname, ushort port)
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            sock.Connect(hostname, port);
            return new ServiceConnection(sock);
        }

        private static ServiceConnection CreateUsingUsbmux(string udid, ushort port, UsbmuxdConnectionType? usbmuxdConnectionType = null)
        {
            UsbmuxdDevice? targetDevice = Usbmux.GetDevice(udid, usbmuxdConnectionType);
            if (targetDevice == null) {
                if (!string.IsNullOrEmpty(udid)) {
                    throw new ConnectionFailedException();
                }
                throw new NoDeviceConnectedException();
            }
            Socket sock = targetDevice.Connect(port);
            return new ServiceConnection(sock, targetDevice);
        }

        private byte[] ReceiveAll(int size)
        {
            if (size <= 0) {
                return Array.Empty<byte>();
            }

            byte[] buffer = new byte[size];
            networkStream.Read(buffer);
            return buffer;
        }

        /// <summary>
        /// Receive a data block prefixed with a u32 length field
        /// </summary>
        /// <returns>The data without the u32 field length as a byte array</returns>
        private byte[] ReceivePrefixed()
        {
            byte[] sizeBytes = ReceiveAll(4);
            if (sizeBytes.Length != 4) {
                return Array.Empty<byte>();
            }

            int size = EndianBitConverter.BigEndian.ToInt32(sizeBytes, 0);
            return ReceiveAll(size);
        }

        private void SendAll(byte[] data)
        {
            networkStream.Write(data);
        }

        private bool UserCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void Close()
        {
            networkStream.Close();
        }

        public static ServiceConnection Create(ConnectionMedium medium, string identifier, ushort port, UsbmuxdConnectionType? usbmuxdConnectionType = null)
        {
            if (medium == ConnectionMedium.TCP) {
                return CreateUsingTcp(identifier, port);
            }
            else {
                return CreateUsingUsbmux(identifier, port, usbmuxdConnectionType);
            }
        }

        public UsbmuxdDevice? GetUsbmuxdDevice()
        {
            return muxDevice;
        }

        public PropertyNode? ReceivePlist()
        {
            byte[] plistBytes = ReceivePrefixed();
            if (plistBytes.Length == 0) {
                return null;
            }

            using (Stream stream = new MemoryStream(plistBytes)) {
                return PropertyList.Load(stream);
            }
        }

        public void SendPlist(PropertyNode data)
        {
            byte[] plistBytes = PropertyList.SaveAsByteArray(data, PlistFormat.Xml);
            byte[] lengthBytes = BitConverter.GetBytes(EndianBitConverter.BigEndian.ToInt32(BitConverter.GetBytes(plistBytes.Length), 0));

            List<byte> payload = new List<byte>();
            payload.AddRange(lengthBytes);
            payload.AddRange(plistBytes);
            SendAll(payload.ToArray());
        }

        public PropertyNode? SendReceivePlist(PropertyNode data)
        {
            SendPlist(data);
            return ReceivePlist();
        }

        public void StartSSL(byte[] certData, byte[] privateKeyData)
        {
            X509Certificate2 cert;
            string tmpPath = Path.GetTempFileName();
            using (FileStream fs = new FileStream(tmpPath, FileMode.OpenOrCreate, FileAccess.ReadWrite)) {
                fs.Write(certData);
                fs.Write(Encoding.UTF8.GetBytes("\n"));
                fs.Write(privateKeyData);
            }
            cert = X509Certificate2.CreateFromPemFile(tmpPath);

            networkStream.Flush();

            SslStream sslStream = new SslStream(networkStream, true, UserCertificateValidationCallback, null, EncryptionPolicy.AllowNoEncryption);
            try {
                sslStream.AuthenticateAsClient(string.Empty, new X509CertificateCollection() { new X509Certificate2(cert.Export(X509ContentType.Pkcs12)) }, SslProtocols.None, false);
            }
            catch (AuthenticationException ex) {
                Debug.WriteLine(ex);
                Debug.WriteLine("SSL authentication failed");
            }

            networkStream = sslStream;
        }
    }
}
