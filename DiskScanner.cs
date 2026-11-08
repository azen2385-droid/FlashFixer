using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

public sealed class DiskCandidate
{
    public uint Number;
    public string Name = "";
    public ulong Size;
    public ushort BusType;
    public bool IsSystem, IsBoot, IsReadOnly;
    public string Serial = "", UniqueId = "", Vid = null, Pid = null;
    public string Source = "";
    public bool IsSystemVerified;

    public string Fingerprint
    {
        get
        {
            using (var sha = SHA256.Create())
            {
                var raw = Size.ToString() + "|" + (Vid ?? "????") + ":" + (Pid ?? "????");
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(h, 0, 8).Replace("-", "");
            }
        }
    }
}

public static class DiskScanner
{
    public const ushort BusUsb = 7;

    enum SystemProbe { Yes, No, Unknown }

    [StructLayout(LayoutKind.Sequential)]
    struct STORAGE_PROPERTY_QUERY
    {
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct STORAGE_DEVICE_DESCRIPTOR
    {
        public int Version;
        public int Size;
        public byte DeviceType;
        public byte DeviceTypeModifier;
        public byte RemovableMedia;
        public byte CommandQueueing;
        public int VendorIdOffset;
        public int ProductIdOffset;
        public int ProductRevisionOffset;
        public int SerialNumberOffset;
        public int BusType;
        public int RawPropertiesLength;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Auto)]
    static extern int CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, uint ulFlags);
    [DllImport("cfgmgr32.dll")]
    static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Auto)]
    static extern int CM_Get_Device_ID(uint dnDevInst, StringBuilder Buffer, int BufferLen, uint ulFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode,
        IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);

    const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;
    const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x560000;
    const uint IOCTL_DISK_GET_LENGTH_INFO = 0x0007405C;
    const int ExtentOffset = 8;
    const int ExtentStride = 24;

    static string[] GetVidPidByParent(string pnpId)
    {
        if (string.IsNullOrEmpty(pnpId)) return null;
        uint devInst;
        if (CM_Locate_DevNode(out devInst, pnpId, 0) != 0) return null;

        for (int depth = 0; depth < 8; depth++)
        {
            var sb = new StringBuilder(512);
            if (CM_Get_Device_ID(devInst, sb, sb.Capacity, 0) != 0) break;
            string id = sb.ToString();
            var m = Regex.Match(id, @"USB\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
            if (m.Success)
                return new[] { m.Groups[1].Value.ToUpperInvariant(), m.Groups[2].Value.ToUpperInvariant() };

            uint parent;
            if (CM_Get_Parent(out parent, devInst, 0) != 0) break;
            devInst = parent;
        }
        return null;
    }

    // Windows pack(8): NumberOfDiskExtents is a DWORD, then 4 bytes of padding,
    // then DISK_EXTENT (24 bytes). Same layout on x86 and x64. Reading at
    // offset 4 on 32-bit misses the system disk.
    static SystemProbe ProbeSystemDisk(int diskNum)
    {
        string sysDrive = Environment.GetEnvironmentVariable("SystemDrive");
        if (string.IsNullOrEmpty(sysDrive)) return SystemProbe.Yes;

        try
        {
            using (var h = CreateFile(@"\\.\" + sysDrive, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return SystemProbe.Unknown;
                IntPtr outPtr = Marshal.AllocHGlobal(1024);
                try
                {
                    uint ret;
                    if (!DeviceIoControl(h, IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
                        IntPtr.Zero, 0, outPtr, 1024, out ret, IntPtr.Zero))
                        return SystemProbe.Unknown;
                    if (ret < 8) return SystemProbe.Unknown;
                    int count = Marshal.ReadInt32(outPtr);
                    if (count <= 0 || count > 32) return SystemProbe.Yes;
                    if (ret < (uint)(ExtentOffset + count * ExtentStride)) return SystemProbe.Yes;
                    for (int i = 0; i < count; i++)
                    {
                        uint n = (uint)Marshal.ReadInt32(outPtr, ExtentOffset + i * ExtentStride);
                        if (n == (uint)diskNum) return SystemProbe.Yes;
                    }
                    return SystemProbe.No;
                }
                finally { Marshal.FreeHGlobal(outPtr); }
            }
        }
        catch { return SystemProbe.Unknown; }
    }

    static bool IsSystemDiskWmi(int diskNum)
    {
        try
        {
            string sysDrive = Environment.GetEnvironmentVariable("SystemDrive");
            if (string.IsNullOrEmpty(sysDrive)) return true;

            string partition = null;
            using (var s = new ManagementObjectSearcher(
                "ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + sysDrive + "'} WHERE AssocClass=Win32_LogicalDiskToPartition"))
            {
                foreach (ManagementObject p in s.Get()) { partition = p["DeviceID"] as string; break; }
            }
            if (partition == null) return true;

            string escaped = partition.Replace("\\", "\\\\").Replace("'", "\\'");
            using (var s = new ManagementObjectSearcher(
                "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + escaped + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
            {
                foreach (ManagementObject d in s.Get())
                    if ((d["Index"]?.ToString() ?? "") == diskNum.ToString()) return true;
            }
            return false;
        }
        catch { return true; }
    }

    static bool ResolveIsSystem(int diskNum, bool wmiSaysSystem)
    {
        var probe = ProbeSystemDisk(diskNum);
        if (probe == SystemProbe.Yes) return true;
        if (wmiSaysSystem) return true;
        if (probe == SystemProbe.No) return false;
        return IsSystemDiskWmi(diskNum);
    }

    public static bool TryGetLength(uint diskNum, out ulong length)
    {
        length = 0;
        uint[] accesses = { 0u, 0x80000000u };
        foreach (uint access in accesses)
        {
            try
            {
                using (var h = CreateFile(@"\\.\PhysicalDrive" + diskNum, access, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                {
                    if (h.IsInvalid) continue;
                    IntPtr p = Marshal.AllocHGlobal(8);
                    try
                    {
                        uint ret;
                        if (!DeviceIoControl(h, IOCTL_DISK_GET_LENGTH_INFO,
                            IntPtr.Zero, 0, p, 8, out ret, IntPtr.Zero) || ret < 8)
                            continue;
                        long v = Marshal.ReadInt64(p);
                        if (v <= 0) return false;
                        length = (ulong)v;
                        return true;
                    }
                    finally { Marshal.FreeHGlobal(p); }
                }
            }
            catch { }
        }
        return false;
    }

    public static List<DiskCandidate> Scan()
    {
        try
        {
            var r = ScanViaMsft();
            if (r != null && r.Count > 0) return r;
        }
        catch { }
        return ScanFallback();
    }

    static List<DiskCandidate> ScanViaMsft()
    {
        var list = new List<DiskCandidate>();
        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
        scope.Connect();

        var pnpByIndex = new Dictionary<uint, string>();
        using (var s = new ManagementObjectSearcher("SELECT Index, PNPDeviceID FROM Win32_DiskDrive"))
            foreach (ManagementObject o in s.Get())
                pnpByIndex[Convert.ToUInt32(o["Index"])] = (o["PNPDeviceID"] as string) ?? "";

        using (var s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM MSFT_Disk")))
            foreach (ManagementObject d in s.Get())
            {
                uint number = Convert.ToUInt32(d["Number"]);
                bool sys = Convert.ToBoolean(d["IsSystem"]);
                bool boot = Convert.ToBoolean(d["IsBoot"]);
                var c = new DiskCandidate
                {
                    Number = number,
                    Name = ((d["FriendlyName"] as string) ?? "").Trim(),
                    Size = Convert.ToUInt64(d["Size"]),
                    BusType = Convert.ToUInt16(d["BusType"]),
                    IsSystem = ResolveIsSystem((int)number, sys || boot),
                    IsBoot = boot || sys,
                    IsReadOnly = Convert.ToBoolean(d["IsReadOnly"]),
                    Serial = ((d["SerialNumber"] as string) ?? "").Trim(),
                    UniqueId = ((d["UniqueId"] as string) ?? "").Trim(),
                    Source = "MSFT",
                    IsSystemVerified = true
                };
                c.IsBoot = c.IsBoot || c.IsSystem;
                string pnp;
                if (pnpByIndex.TryGetValue(c.Number, out pnp))
                {
                    var vp = GetVidPidByParent(pnp);
                    if (vp != null) { c.Vid = vp[0]; c.Pid = vp[1]; }
                }
                list.Add(c);
            }
        return list;
    }

    static List<DiskCandidate> ScanFallback()
    {
        var list = new List<DiskCandidate>();
        using (var s = new ManagementObjectSearcher(
            "SELECT Index, Model, Size, PNPDeviceID, SerialNumber, InterfaceType FROM Win32_DiskDrive"))
        foreach (ManagementObject o in s.Get())
        {
            uint num = Convert.ToUInt32(o["Index"]);
            string pnp = (o["PNPDeviceID"] as string) ?? "";
            string iface = (o["InterfaceType"] as string) ?? "";

            ushort bus = GetBusTypeViaIoctl((int)num);
            if (bus == 0)
            {
                bool isUsb = iface.Equals("USB", StringComparison.OrdinalIgnoreCase)
                          || pnp.StartsWith("USBSTOR\\", StringComparison.OrdinalIgnoreCase)
                          || pnp.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase);
                bus = isUsb ? BusUsb : (ushort)0;
            }

            bool isSys = ResolveIsSystem((int)num, false);

            var c = new DiskCandidate
            {
                Number = num,
                Name = ((o["Model"] as string) ?? "").Trim(),
                Serial = ((o["SerialNumber"] as string) ?? "").Trim(),
                BusType = bus,
                IsSystem = isSys,
                IsBoot = isSys,
                Source = "Fallback-Win32",
                IsSystemVerified = true
            };
            try { c.Size = Convert.ToUInt64(o["Size"]); } catch { c.Size = 0; }

            var vp = GetVidPidByParent(pnp);
            if (vp != null) { c.Vid = vp[0]; c.Pid = vp[1]; }
            list.Add(c);
        }
        return list;
    }

    static ushort GetBusTypeViaIoctl(int diskNum)
    {
        try
        {
            using (var h = CreateFile(@"\\.\PhysicalDrive" + diskNum, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return 0;
                var query = new STORAGE_PROPERTY_QUERY { PropertyId = 0, QueryType = 0 };
                IntPtr inPtr = Marshal.AllocHGlobal(Marshal.SizeOf(query));
                Marshal.StructureToPtr(query, inPtr, false);
                IntPtr outPtr = Marshal.AllocHGlobal(1024);
                try
                {
                    uint ret;
                    if (!DeviceIoControl(h, IOCTL_STORAGE_QUERY_PROPERTY,
                        inPtr, (uint)Marshal.SizeOf(query), outPtr, 1024, out ret, IntPtr.Zero))
                        return 0;
                    var desc = (STORAGE_DEVICE_DESCRIPTOR)Marshal.PtrToStructure(outPtr, typeof(STORAGE_DEVICE_DESCRIPTOR));
                    return (ushort)desc.BusType;
                }
                finally
                {
                    Marshal.FreeHGlobal(inPtr);
                    Marshal.FreeHGlobal(outPtr);
                }
            }
        }
        catch { return 0; }
    }
}
