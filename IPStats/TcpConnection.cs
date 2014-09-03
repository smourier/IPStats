using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace IPStats
{
    public sealed class TcpConnection : INotifyPropertyChanged, IEquatable<TcpConnection>
    {
        private Process _process;
        private ValueType _row;

        public event PropertyChangedEventHandler PropertyChanged;

        private TcpConnection(ValueType row)
        {
            _row = row;
        }

        public ulong SegmentsOut
        {
            get
            {
                TCP_ESTATS_DATA_ROD_v0 rod;
                if (!DataStatsEnabled || !TryGetRodStats<TCP_ESTATS_DATA_ROD_v0>(TCP_ESTATS_TYPE.TcpConnectionEstatsData, out rod))
                    return 0;

                return rod.SegsOut;
            }
        }

        public ulong SegmentsIn
        {
            get
            {
                TCP_ESTATS_DATA_ROD_v0 rod;
                if (!DataStatsEnabled || !TryGetRodStats<TCP_ESTATS_DATA_ROD_v0>(TCP_ESTATS_TYPE.TcpConnectionEstatsData, out rod))
                    return 0;

                return rod.SegsIn;
            }
        }

        public ulong DataBytesIn
        {
            get
            {
                TCP_ESTATS_DATA_ROD_v0 rod;
                if (!DataStatsEnabled || !TryGetRodStats<TCP_ESTATS_DATA_ROD_v0>(TCP_ESTATS_TYPE.TcpConnectionEstatsData, out rod))
                    return 0;

                return rod.DataBytesIn;
            }
        }

        public ulong DataBytesOut
        {
            get
            {
                TCP_ESTATS_DATA_ROD_v0 rod;
                if (!DataStatsEnabled || !TryGetRodStats<TCP_ESTATS_DATA_ROD_v0>(TCP_ESTATS_TYPE.TcpConnectionEstatsData, out rod))
                    return 0;

                return rod.DataBytesOut;
            }
        }

        public bool DataStatsEnabled
        {
            get
            {
                return GetStatsState(TCP_ESTATS_TYPE.TcpConnectionEstatsData, false);
            }
            set
            {
                if (value != DataStatsEnabled)
                {
                    EnableStats(TCP_ESTATS_TYPE.TcpConnectionEstatsData, value);
                    OnPropertyChanged("DataStatsEnabled");
                }
            }
        }

        public ulong InboundBandwidth
        {
            get
            {
                TCP_ESTATS_BANDWIDTH_ROD_v0 rod;
                if (!InboundBandwidthStatsEnabled || !TryGetRodStats<TCP_ESTATS_BANDWIDTH_ROD_v0>(TCP_ESTATS_TYPE.TcpConnectionEstatsBandwidth, out rod))
                    return 0;

                return rod.InboundBandwidth;
            }
        }

        public bool InboundBandwidthStatsEnabled
        {
            get
            {
                return GetStatsState(TCP_ESTATS_TYPE.TcpConnectionEstatsBandwidth, 2, false)[1] == TCP_BOOLEAN_OPTIONAL.TcpBoolOptEnabled;
            }
            set
            {
                if (value != InboundBandwidthStatsEnabled)
                {
                    EnableStats(TCP_ESTATS_TYPE.TcpConnectionEstatsBandwidth, new TCP_BOOLEAN_OPTIONAL[] { TCP_BOOLEAN_OPTIONAL.TcpBoolOptUnchanged, value ? TCP_BOOLEAN_OPTIONAL.TcpBoolOptEnabled : TCP_BOOLEAN_OPTIONAL.TcpBoolOptDisabled });
                    OnPropertyChanged("InboundBandwidthStatsEnabled");
                }
            }
        }

        public ulong OutboundBandwidth
        {
            get
            {
                TCP_ESTATS_BANDWIDTH_ROD_v0 rod;
                if (!OutboundBandwidthStatsEnabled || !TryGetRodStats<TCP_ESTATS_BANDWIDTH_ROD_v0>(TCP_ESTATS_TYPE.TcpConnectionEstatsBandwidth, out rod))
                    return 0;

                return rod.OutboundBandwidth;
            }
        }

        public bool OutboundBandwidthStatsEnabled
        {
            get
            {
                return GetStatsState(TCP_ESTATS_TYPE.TcpConnectionEstatsBandwidth, 2, false)[0] == TCP_BOOLEAN_OPTIONAL.TcpBoolOptEnabled;
            }
            set
            {
                if (value != OutboundBandwidthStatsEnabled)
                {
                    EnableStats(TCP_ESTATS_TYPE.TcpConnectionEstatsBandwidth, new TCP_BOOLEAN_OPTIONAL[] { value ? TCP_BOOLEAN_OPTIONAL.TcpBoolOptEnabled : TCP_BOOLEAN_OPTIONAL.TcpBoolOptDisabled, TCP_BOOLEAN_OPTIONAL.TcpBoolOptUnchanged });
                    OnPropertyChanged("OutboundBandwidthStatsEnabled");
                }
            }
        }

        public Process Process
        {
            get
            {
                if (_process == null && ProcessId != 0)
                {
                    try
                    {
                        _process = Process.GetProcessById(ProcessId);
                    }
                    catch
                    {
                        // happens...
                    }
                }
                return _process;
            }
        }

        public int ProcessId { get; private set; }
        public IPEndPoint LocalEndPoint { get; private set; }
        public IPEndPoint RemoteEndPoint { get; private set; }
        public TcpState State { get; private set; }
        public bool HasChanged { get; private set; }

        public string ProtocolVersion
        {
            get
            {
                return LocalEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? "V6" : "V4";
            }
        }

        private void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public override string ToString()
        {
            return State + ":" + LocalEndPoint + " -> " + RemoteEndPoint;
        }

        public bool Equals(TcpConnection other)
        {
            if (other == null)
                return false;

            return State == other.State && LocalEndPoint.Equals(other.LocalEndPoint) && RemoteEndPoint.Equals(other.RemoteEndPoint);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TcpConnection);
        }

        public override int GetHashCode()
        {
            return State.GetHashCode() ^ LocalEndPoint.GetHashCode() ^ RemoteEndPoint.GetHashCode();
        }

        public static IList<TcpConnection> GetAll()
        {
            return GetAll(false);
        }

        public static IList<TcpConnection> GetAll(bool observable)
        {
            IList<TcpConnection> list = observable ? (IList<TcpConnection>)new ObservableCollection<TcpConnection>() : (IList<TcpConnection>)new List<TcpConnection>();
            if (Socket.OSSupportsIPv4)
            {
                Add(list, AF_INET);
            }
            if (Socket.OSSupportsIPv6)
            {
                Add(list, AF_INET6);
            }
            return list;
        }

        private static void Add(IList<TcpConnection> list, int af)
        {
            int pdwSize = 0;
            int hr = GetExtendedTcpTable(IntPtr.Zero, ref pdwSize, false, af, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
            if (hr != ERROR_INSUFFICIENT_BUFFER)
                throw new Win32Exception(hr);

            IntPtr pTcpTable = Marshal.AllocCoTaskMem(pdwSize);
            try
            {
                hr = GetExtendedTcpTable(pTcpTable, ref pdwSize, false, af, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                if (hr != 0)
                    throw new Win32Exception(hr);

                IntPtr ptr = pTcpTable;
                int structure = Marshal.ReadInt32(ptr);
                ptr += Marshal.SizeOf(structure);
                for (int i = 0; i < structure; i++)
                {
                    TcpConnection connection;
                    if (af == AF_INET6)
                    {
                        MIB_TCP6ROW_OWNER_PID entry = (MIB_TCP6ROW_OWNER_PID)Marshal.PtrToStructure(ptr, typeof(MIB_TCP6ROW_OWNER_PID));
                        ptr += Marshal.SizeOf(entry);
                        MIB_TCP6ROW row = new MIB_TCP6ROW
                        {
                            dwLocalScopeId = entry.dwLocalScopeId,
                            dwRemoteScopeId = entry.dwRemoteScopeId,
                            localPort1 = entry.localPort1,
                            localPort2 = entry.localPort2,
                            localPort3 = entry.localPort3,
                            localPort4 = entry.localPort4,
                            remotePort1 = entry.remotePort1,
                            remotePort2 = entry.remotePort2,
                            remotePort3 = entry.remotePort3,
                            remotePort4 = entry.remotePort4,
                            dwState = entry.dwState,
                            ucLocalAddr = entry.ucLocalAddr,
                            ucRemoteAddr = entry.ucRemoteAddr
                        };

                        connection = new TcpConnection(row);
                        connection.ProcessId = entry.dwOwningPid;
                        connection.State = entry.dwState;
                        connection.LocalEndPoint = new IPEndPoint(new IPAddress(entry.ucLocalAddr, entry.dwLocalScopeId), (entry.localPort1 << 8) | entry.localPort2);
                        connection.RemoteEndPoint = new IPEndPoint(new IPAddress(entry.ucRemoteAddr, entry.dwRemoteScopeId), (entry.remotePort1 << 8) | entry.remotePort2);
                    }
                    else
                    {
                        MIB_TCPROW_OWNER_PID entry = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(ptr, typeof(MIB_TCPROW_OWNER_PID));
                        ptr += Marshal.SizeOf(entry);
                        MIB_TCPROW mib_tcprow2 = new MIB_TCPROW
                        {
                            localPort1 = entry.localPort1,
                            localPort2 = entry.localPort2,
                            localPort3 = entry.localPort3,
                            localPort4 = entry.localPort4,
                            remotePort1 = entry.remotePort1,
                            remotePort2 = entry.remotePort2,
                            remotePort3 = entry.remotePort3,
                            remotePort4 = entry.remotePort4,
                            dwState = entry.dwState,
                            dwLocalAddr = entry.dwLocalAddr,
                            dwRemoteAddr = entry.dwRemoteAddr
                        };

                        connection = new TcpConnection(mib_tcprow2);
                        connection.ProcessId = entry.dwOwningPid;
                        connection.State = entry.dwState;
                        connection.LocalEndPoint = new IPEndPoint(entry.dwLocalAddr, (entry.localPort1 << 8) | entry.localPort2);
                        connection.RemoteEndPoint = new IPEndPoint(entry.dwRemoteAddr, (entry.remotePort1 << 8) | entry.remotePort2);
                    }
                    list.Add(connection);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pTcpTable);
            }
        }

        private void EnableStats(TCP_ESTATS_TYPE type, bool enable)
        {
            IntPtr ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(_row.GetType()));
            try
            {
                int hr;
                Marshal.StructureToPtr(_row, ptr, false);
                if (LocalEndPoint.AddressFamily == AddressFamily.InterNetwork)
                {
                    hr = SetPerTcpConnectionEStats(ptr, type, ref enable, 0, 1, 0);
                }
                else
                {
                    hr = SetPerTcp6ConnectionEStats(ptr, type, ref enable, 0, 1, 0);
                }

                if (hr != 0)
                    throw new Win32Exception(hr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }

        private void EnableStats(TCP_ESTATS_TYPE type, params TCP_BOOLEAN_OPTIONAL[] options)
        {
            IntPtr ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(_row.GetType()));
            try
            {
                int hr;
                Marshal.StructureToPtr(_row, ptr, false);
                if (LocalEndPoint.AddressFamily == AddressFamily.InterNetwork)
                {
                    hr = SetPerTcpConnectionEStats(ptr, type, options, 0, options.Length * 4, 0);
                }
                else
                {
                    hr = SetPerTcp6ConnectionEStats(ptr, type, options, 0, options.Length * 4, 0);
                }

                if (hr != 0)
                    throw new Win32Exception(hr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }

        private bool GetStatsState(TCP_ESTATS_TYPE type, bool throwOnError)
        {
            bool flag;
            IntPtr ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(_row.GetType()));
            try
            {
                int hr;
                byte state;
                Marshal.StructureToPtr(_row, ptr, false);
                if (LocalEndPoint.AddressFamily == AddressFamily.InterNetwork)
                {
                    hr = GetPerTcpConnectionEStats(ptr, type, out state, 0, 1, IntPtr.Zero, 0, 0, IntPtr.Zero, 0, 0);
                }
                else
                {
                    hr = GetPerTcp6ConnectionEStats(ptr, type, out state, 0, 1, IntPtr.Zero, 0, 0, IntPtr.Zero, 0, 0);
                }
                
                if (hr != 0 && throwOnError)
                    throw new Win32Exception(hr);

                flag = state != 0;
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
            return flag;
        }

        private TCP_BOOLEAN_OPTIONAL[] GetStatsState(TCP_ESTATS_TYPE type, int count, bool throwOnError)
        {
            IntPtr ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(_row.GetType()));
            try
            {
                int hr;
                Marshal.StructureToPtr(_row, ptr, false);
                TCP_BOOLEAN_OPTIONAL[] options = new TCP_BOOLEAN_OPTIONAL[count];
                for (int i = 0; i < options.Length; i++)
                {
                    options[i] = TCP_BOOLEAN_OPTIONAL.TcpBoolOptUnchanged;
                }

                if (LocalEndPoint.AddressFamily == AddressFamily.InterNetwork)
                {
                    hr = GetPerTcpConnectionEStats(ptr, type, options, 0, count * 4, IntPtr.Zero, 0, 0, IntPtr.Zero, 0, 0);
                }
                else
                {
                    hr = GetPerTcp6ConnectionEStats(ptr, type, options, 0, count * 4, IntPtr.Zero, 0, 0, IntPtr.Zero, 0, 0);
                }

                if (hr != 0 && throwOnError)
                    throw new Win32Exception(hr);

                return options;
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }

        private bool TryGetRodStats<T>(TCP_ESTATS_TYPE type, out T value)
        {
            IntPtr rod = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(T)));
            try
            {
                IntPtr ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(_row.GetType()));
                try
                {
                    int hr;
                    Marshal.StructureToPtr(_row, ptr, false);
                    if (LocalEndPoint.AddressFamily == AddressFamily.InterNetwork)
                    {
                        hr = GetPerTcpConnectionEStats(ptr, type, IntPtr.Zero, 0, 0, IntPtr.Zero, 0, 0, rod, 0, Marshal.SizeOf(typeof(T)));
                    }
                    else
                    {
                        hr = GetPerTcp6ConnectionEStats(ptr, type, IntPtr.Zero, 0, 0, IntPtr.Zero, 0, 0, rod, 0, Marshal.SizeOf(typeof(T)));
                    }

                    if (hr != 0)
                    {
                        value = default(T);
                        return false;
                    }
                    
                    value = (T)Marshal.PtrToStructure(rod, typeof(T));
                    return true;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(rod);
            }
        }

        private void Update()
        {
            if (DataStatsEnabled)
            {
                OnPropertyChanged("DataBytesIn");
                OnPropertyChanged("DataBytesOut");
                OnPropertyChanged("SegmentsIn");
                OnPropertyChanged("SegmentsOut");
            }
            if (InboundBandwidthStatsEnabled)
            {
                OnPropertyChanged("InboundBandwidth");
            }
            if (OutboundBandwidthStatsEnabled)
            {
                OnPropertyChanged("OutboundBandwidth");
            }
        }

        public static void Update(IList<TcpConnection> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            List<TcpConnection> all = (List<TcpConnection>)GetAll(false);
            List<TcpConnection> removed = new List<TcpConnection>(list);
            foreach (var cnx in list)
            {
                TcpConnection item = list.FirstOrDefault<TcpConnection>(c => c.Equals(cnx));
                if (item != null)
                {
                    item.Update();
                    removed.Remove(item);
                }
                else
                {
                    list.Add(cnx);
                }
            }

            foreach (var cnx in removed)
            {
                list.Remove(cnx);
            }
        }

        [DllImport("iphlpapi.dll")]
        private static extern int GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, TCP_TABLE_CLASS TableClass, int Reserved);
        [DllImport("iphlpapi.dll")]
        private static extern int GetPerTcp6ConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, TCP_BOOLEAN_OPTIONAL[] Rw, int RwVersion, int RwSize, IntPtr Ros, int RosVersion, int RosSize, IntPtr Rod, int RodVersion, int RodSize);
        [DllImport("iphlpapi.dll")]
        private static extern int GetPerTcp6ConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, out byte Rw, int RwVersion, int RwSize, IntPtr Ros, int RosVersion, int RosSize, IntPtr Rod, int RodVersion, int RodSize);
        [DllImport("iphlpapi.dll")]
        private static extern int GetPerTcp6ConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, IntPtr Rw, int RwVersion, int RwSize, IntPtr Ros, int RosVersion, int RosSize, IntPtr Rod, int RodVersion, int RodSize);
        [DllImport("iphlpapi.dll")]
        private static extern int GetPerTcpConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, TCP_BOOLEAN_OPTIONAL[] Rw, int RwVersion, int RwSize, IntPtr Ros, int RosVersion, int RosSize, IntPtr Rod, int RodVersion, int RodSize);
        [DllImport("iphlpapi.dll")]
        private static extern int GetPerTcpConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, out byte Rw, int RwVersion, int RwSize, IntPtr Ros, int RosVersion, int RosSize, IntPtr Rod, int RodVersion, int RodSize);
        [DllImport("iphlpapi.dll")]
        private static extern int GetPerTcpConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, IntPtr Rw, int RwVersion, int RwSize, IntPtr Ros, int RosVersion, int RosSize, IntPtr Rod, int RodVersion, int RodSize);
        [DllImport("iphlpapi.dll")]
        private static extern int SetPerTcp6ConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, TCP_BOOLEAN_OPTIONAL[] Rw, int RwVersion, int RwSize, int Offset);
        [DllImport("iphlpapi.dll")]
        private static extern int SetPerTcp6ConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, ref bool Rw, int RwVersion, int RwSize, int Offset);
        [DllImport("iphlpapi.dll")]
        private static extern int SetPerTcpConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, TCP_BOOLEAN_OPTIONAL[] Rw, int RwVersion, int RwSize, int Offset);
        [DllImport("iphlpapi.dll")]
        private static extern int SetPerTcpConnectionEStats(IntPtr Row, TCP_ESTATS_TYPE EstatsType, ref bool Rw, int RwVersion, int RwSize, int Offset);

        private const int AF_INET = 2;
        private const int AF_INET6 = 23;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCP6ROW
        {
            public TcpState dwState;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public int dwLocalScopeId;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucRemoteAddr;
            public int dwRemoteScopeId;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public int dwLocalScopeId;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucRemoteAddr;
            public int dwRemoteScopeId;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
            public TcpState dwState;
            public int dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW
        {
            public TcpState dwState;
            public uint dwLocalAddr;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            public uint dwRemoteAddr;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public TcpState dwState;
            public uint dwLocalAddr;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            public uint dwRemoteAddr;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
            public int dwOwningPid;
        }

        private enum TCP_BOOLEAN_OPTIONAL
        {
            TcpBoolOptDisabled = 0,
            TcpBoolOptEnabled = 1,
            TcpBoolOptUnchanged = -1
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TCP_ESTATS_BANDWIDTH_ROD_v0
        {
            public ulong OutboundBandwidth;
            public ulong InboundBandwidth;
            public ulong OutboundInstability;
            public ulong InboundInstability;
            public bool OutboundBandwidthPeaked;
            public bool InboundBandwidthPeaked;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TCP_ESTATS_DATA_ROD_v0
        {
            public ulong DataBytesOut;
            public ulong DataSegsOut;
            public ulong DataBytesIn;
            public ulong DataSegsIn;
            public ulong SegsOut;
            public ulong SegsIn;
            public uint SoftErrors;
            public uint SoftErrorReason;
            public uint SndUna;
            public uint SndNxt;
            public uint SndMax;
            public ulong ThruBytesAcked;
            public uint RcvNxt;
            public ulong ThruBytesReceived;
        }

        private enum TCP_ESTATS_TYPE
        {
            TcpConnectionEstatsSynOpts,
            TcpConnectionEstatsData,
            TcpConnectionEstatsSndCong,
            TcpConnectionEstatsPath,
            TcpConnectionEstatsSendBuff,
            TcpConnectionEstatsRec,
            TcpConnectionEstatsObsRec,
            TcpConnectionEstatsBandwidth,
            TcpConnectionEstatsFineRtt,
            TcpConnectionEstatsMaximum
        }

        private enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }
    }
}
