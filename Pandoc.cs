namespace Pandoc
{
    using System;
    using System.IO;
    using System.Text;
    using System.Runtime.InteropServices;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int Reader(IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void Writer(IntPtr data, int length);

    class Native
    {
        [DllImport("libpandoc", CallingConvention=CallingConvention.Cdecl)]
        public static extern void pandoc_init();
        
        [DllImport("libpandoc", CallingConvention=CallingConvention.Cdecl)]
        public static extern void pandoc_exit();

        [DllImport("libpandoc", CallingConvention=CallingConvention.Cdecl)]
        public static extern string pandoc(int bufferSize,
                                           byte[] inputFormat,
                                           byte[] outputFormat,
                                           byte[] settings, 
                                           Reader reader, Writer writer);
    }

    public class PandocException : Exception 
    {
        public PandocException(string message) : base(message)
        {                       
        }
    }

    public class Processor : IDisposable
    {
        const int charSize     = 1024;
        const int bytesPerChar = 4;
        const int byteSize     = bytesPerChar * charSize;
        char[] chars;
        byte[] bytes;        
        Encoding encoding;
                
        public Processor()
        {
            chars = new char[charSize];
            bytes = new byte[byteSize];
            encoding = Encoding.UTF8;
            Native.pandoc_init();
        }
                
        public void Process(string source, string target, 
                            TextReader input, TextWriter output)
        {
            Process(source, target, null, input, output);
        }

        byte[] Bytes(string text)
        {
            if (text != null) {
                byte[] bytes = encoding.GetBytes(text);
                byte[] result = new byte[bytes.Length + 1];
                for (var i = 0; i < bytes.Length; i++) {
                    result[i] = bytes[i];
                }
                return result;
            } else {
                return new byte[0];
            }
        }
                               
        public void Process(string source, string target, string config,
                            TextReader input, TextWriter output)
        {
            Reader reader =
                delegate (IntPtr data)
                {
                    int c = input.ReadBlock(chars, 0, charSize);
                    int b = encoding.GetBytes(chars, 0, c, bytes, 0);
                    Marshal.Copy(bytes, 0, data, b);
                    return b;
                };
            Writer writer =
                delegate (IntPtr data, int length) 
                {
                    if (length > 0) {
                        Marshal.Copy(data, bytes, 0, length);
                        int c = encoding.GetChars(bytes, 0, length, chars, 0);
                        output.Write(chars, 0, c);
                    }
                };
            string err = Native.pandoc(byteSize,
                                       Bytes(source),
                                       Bytes(target),
                                       Bytes(config),
                                       reader,
                                       writer);
            if (err != null) {
                throw new PandocException(err);
            }
        }
        
        public void Dispose()
        {
            Native.pandoc_exit();
        }
    }
}
