﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpUtils;

namespace CSPspEmu.Hle.Vfs.Local
{
    public class HleIoDriverLocalFileSystem : IHleIoDriver
    {
        public string BasePath { get; protected set; }

        public HleIoDriverLocalFileSystem(string BasePath)
        {
            this.BasePath = BasePath;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Path"></param>
        /// <returns></returns>
        public static string GetSanitizedPath(string Path)
        {
            var Parts = new Stack<string>();
            foreach (var Part in Path.Replace('\\', '/').Split('/'))
            {
                switch (Part)
                {
                    case "":
                        if (Parts.Count == 0) Parts.Push("");
                        break;
                    case ".": break;
                    case "..":
                        if (Parts.Count > 0) Parts.Pop();
                        break;
                    default:
                        Parts.Push(Part);
                        break;
                }
            }

            return String.Join("/", Parts.Reverse());
        }

        protected string GetFullNormalizedAndSanitizedPath(string Path)
        {
            var Normalized = BasePath + "/" + GetSanitizedPath(Path);
            Normalized = Normalized.Replace('\\', '/').Replace("//", "/");
            return Normalized;
        }

        public unsafe int IoInit()
        {
            return 0;
        }

        public unsafe int IoExit()
        {
            return 0;
        }

        public unsafe int IoOpen(HleIoDrvFileArg HleIoDrvFileArg, string FileName, HleIoFlags Flags, SceMode Mode)
        {
            var RealFileName = GetFullNormalizedAndSanitizedPath(FileName);
            FileMode FileMode = FileMode.Open;
            FileAccess FileAccess = 0;
            bool Append = Flags.HasFlag(HleIoFlags.Append);
            bool Read = Flags.HasFlag(HleIoFlags.Read);
            bool Write = Flags.HasFlag(HleIoFlags.Write);
            bool Truncate = Flags.HasFlag(HleIoFlags.Truncate);
            bool Create = Flags.HasFlag(HleIoFlags.Create);

            if (Read) FileAccess |= FileAccess.Read;
            if (Write) FileAccess |= FileAccess.Write;

            if (Append)
            {
                FileMode = FileMode.OpenOrCreate;
            }
            else if (Create)
            {
                FileMode = FileMode.Create;
            }
            else if (Truncate)
            {
                FileMode = FileMode.Truncate;
            }

            //if (Append) FileMode |= FileMode.Open;

            var Stream = File.Open(RealFileName, FileMode, FileAccess, FileShare.Delete | FileShare.ReadWrite);
            HleIoDrvFileArg.FileArgument = Stream;

            if (Append)
            {
                Stream.Position = Stream.Length;
            }

            return 0;
        }

        public unsafe int IoClose(HleIoDrvFileArg HleIoDrvFileArg)
        {
            var FileStream = ((FileStream) HleIoDrvFileArg.FileArgument);
            FileStream.Close();
            return 0;
        }

        public unsafe int IoRead(HleIoDrvFileArg HleIoDrvFileArg, byte* OutputPointer, int OutputLength)
        {
            try
            {
                var Buffer = new byte[OutputLength];
                var FileStream = ((FileStream) HleIoDrvFileArg.FileArgument);
                //Console.WriteLine("ReadPosition: {0}", FileStream.Position);
                int Readed = FileStream.Read(Buffer, 0, OutputLength);
                for (int n = 0; n < Readed; n++) *OutputPointer++ = Buffer[n];
                return Readed;
            }
            catch (Exception Exception)
            {
                Console.WriteLine(Exception);
                return -1;
            }
        }

        public unsafe int IoWrite(HleIoDrvFileArg HleIoDrvFileArg, byte* InputPointer, int InputLength)
        {
            try
            {
                var Buffer = new byte[InputLength];
                for (int n = 0; n < InputLength; n++) Buffer[n] = *InputPointer++;

                var FileStream = ((FileStream) HleIoDrvFileArg.FileArgument);
                FileStream.Write(Buffer, 0, InputLength);
                FileStream.Flush();
                return InputLength;
            }
            catch (Exception)
            {
                //Console.Error.WriteLine(Exception);
                return -1;
            }
        }

        public unsafe long IoLseek(HleIoDrvFileArg HleIoDrvFileArg, long Offset, SeekAnchor Whence)
        {
            var FileStream = ((FileStream) HleIoDrvFileArg.FileArgument);
            switch (Whence)
            {
                case SeekAnchor.Set:
                    FileStream.Position = Offset;
                    break;
                case SeekAnchor.Cursor:
                    FileStream.Position = FileStream.Position + Offset;
                    break;
                case SeekAnchor.End:
                    FileStream.Position = FileStream.Length + Offset;
                    break;
            }
            return FileStream.Position;
        }

        public unsafe int IoIoctl(HleIoDrvFileArg HleIoDrvFileArg, uint Command, byte* InputPointer, int InputLength,
            byte* OutputPointer, int OutputLength)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoRemove(HleIoDrvFileArg HleIoDrvFileArg, string Name)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoMkdir(HleIoDrvFileArg HleIoDrvFileArg, string Name, SceMode Mode)
        {
            var RealFileName = GetFullNormalizedAndSanitizedPath(Name);
            Directory.CreateDirectory(RealFileName);
            //HleIoDrvFileArg.
            //throw new NotImplementedException();
            return 0;
        }

        public unsafe int IoRmdir(HleIoDrvFileArg HleIoDrvFileArg, string Name)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoDopen(HleIoDrvFileArg HleIoDrvFileArg, string Name)
        {
            var RealFileName = GetFullNormalizedAndSanitizedPath(Name);
            //Console.Error.WriteLine("'{0}'", RealFileName);
            var Items = new List<HleIoDirent>();

            Items.Add(CreateFakeDirectoryHleIoDirent(RealFileName, "."));
            Items.Add(CreateFakeDirectoryHleIoDirent(RealFileName, ".."));
            Items.AddRange(new DirectoryInfo(RealFileName).EnumerateFiles()
                .Select(Item => ConvertFileSystemInfoToHleIoDirent(Item)));
            Items.AddRange(new DirectoryInfo(RealFileName).EnumerateDirectories()
                .Select(Item => ConvertFileSystemInfoToHleIoDirent(Item)));

            //HleIoDrvFileArg.FileArgument = new DisposableDummy<DirectoryEnumerator<HleIoDirent>>(new DirectoryEnumerator<HleIoDirent>(Items.ToArray()));
            HleIoDrvFileArg.FileArgument = new DirectoryEnumerator<HleIoDirent>(Items.ToArray());
            return 0;
        }

        public unsafe int IoDclose(HleIoDrvFileArg HleIoDrvFileArg)
        {
            //throw new NotImplementedException();
            return 0;
        }

        public unsafe static HleIoDirent CreateFakeDirectoryHleIoDirent(string RealFileName, string Name)
        {
            var Ret = ConvertFileSystemInfoToHleIoDirent(new DirectoryInfo(RealFileName + "\\" + Name));
            Ret.Name = Name;
            return Ret;
        }

        public unsafe static HleIoDirent ConvertFileSystemInfoToHleIoDirent(FileSystemInfo FileSystemInfo)
        {
            var HleIoDirent = default(HleIoDirent);
            var FileInfo = (FileSystemInfo as FileInfo);
            var DirectoryInfo = (FileSystemInfo as DirectoryInfo);
            {
                if (DirectoryInfo != null)
                {
                    HleIoDirent.Stat.Size = 4096;
                    HleIoDirent.Stat.Mode = (SceMode) 4605;
                    HleIoDirent.Stat.Attributes = IOFileModes.Directory;
                }
                else
                {
                    HleIoDirent.Stat.Size = FileInfo.Length;
                    HleIoDirent.Stat.Mode = (SceMode) 8628;
                    HleIoDirent.Stat.Attributes = IOFileModes.File;
                }
                HleIoDirent.Name = FileSystemInfo.Name.ToLower();

                HleIoDirent.Stat.TimeCreation = ScePspDateTime.FromDateTime(FileSystemInfo.CreationTime);
                HleIoDirent.Stat.TimeLastAccess = ScePspDateTime.FromDateTime(FileSystemInfo.LastAccessTime);
                HleIoDirent.Stat.TimeLastModification = ScePspDateTime.FromDateTime(FileSystemInfo.LastWriteTime);

                HleIoDirent.Stat.DeviceDependentData0 = 10;
            }
            return HleIoDirent;
        }

        public unsafe int IoDread(HleIoDrvFileArg HleIoDrvFileArg, HleIoDirent* IoDirent)
        {
            //var Enumerator = (DirectoryEnumerator<HleIoDirent>)(DisposableDummy<DirectoryEnumerator<HleIoDirent>>)HleIoDrvFileArg.FileArgument;
            var Enumerator = (DirectoryEnumerator<HleIoDirent>) HleIoDrvFileArg.FileArgument;

            // More items.
            if (Enumerator.MoveNext())
            {
                //Console.Error.WriteLine("'{0}'", Enumerator.Current.ToString());
                *IoDirent = Enumerator.Current;
                /*
                
                */
            }
            // No more items.
            else
            {
            }

            return Enumerator.GetLeft();
        }

        public unsafe int IoGetstat(HleIoDrvFileArg HleIoDrvFileArg, string FileName, SceIoStat* Stat)
        {
            var RealFileName = GetFullNormalizedAndSanitizedPath(FileName);
            //Console.WriteLine(RealFileName);

            Stat->Attributes = IOFileModes.CanExecute | IOFileModes.CanRead | IOFileModes.CanWrite;
            Stat->Mode = 0;
            Stat->Mode = SceMode.UserCanExecute | SceMode.UserCanRead | SceMode.UserCanWrite;
            Stat->Mode = SceMode.GroupCanExecute | SceMode.GroupCanRead | SceMode.GroupCanWrite;
            Stat->Mode = SceMode.OtherCanExecute | SceMode.OtherCanRead | SceMode.OtherCanWrite;

            FileSystemInfo FileSystemInfo = null;
            if (File.Exists(RealFileName))
            {
                var FileInfo = new FileInfo(RealFileName);
                FileSystemInfo = FileInfo;
                Stat->Size = FileInfo.Length;
                Stat->Mode |= SceMode.File;
                Stat->Attributes |= IOFileModes.File;
            }
            else if (Directory.Exists(RealFileName))
            {
                var DirectoryInfo = new DirectoryInfo(RealFileName);
                FileSystemInfo = DirectoryInfo;
                Stat->Mode |= SceMode.Directory;
                Stat->Attributes |= IOFileModes.Directory;
            }
            else
            {
                throw(new FileNotFoundException("Can't find file '" + RealFileName + "'"));
            }

            Stat->TimeCreation = ScePspDateTime.FromDateTime(FileSystemInfo.CreationTimeUtc);
            Stat->TimeLastAccess = ScePspDateTime.FromDateTime(FileSystemInfo.LastAccessTimeUtc);
            Stat->TimeLastModification = ScePspDateTime.FromDateTime(FileSystemInfo.LastWriteTimeUtc);

            return 0;
        }

        public unsafe int IoChstat(HleIoDrvFileArg HleIoDrvFileArg, string FileName, SceIoStat* stat, int bits)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoRename(HleIoDrvFileArg HleIoDrvFileArg, string OldFileName, string NewFileName)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoChdir(HleIoDrvFileArg HleIoDrvFileArg, string DirectoryName)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoMount(HleIoDrvFileArg HleIoDrvFileArg)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoUmount(HleIoDrvFileArg HleIoDrvFileArg)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoDevctl(HleIoDrvFileArg HleIoDrvFileArg, string DeviceName, uint Command, byte* InputPointer,
            int InputLength, byte* OutputPointer, int OutputLength)
        {
            throw new NotImplementedException();
        }

        public unsafe int IoUnk21(HleIoDrvFileArg HleIoDrvFileArg)
        {
            throw new NotImplementedException();
        }
    }
}