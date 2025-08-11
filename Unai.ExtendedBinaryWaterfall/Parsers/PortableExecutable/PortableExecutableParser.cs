using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.PortableExecutable;

public enum PeDataDirectory
{
	ExportTable,
	ImportTable,
	ResourceTable,
	ExceptionTable,
	SecurityTable,
	BaseRelocationTable,
	Debug,
	Description,
	GlobalPointer,
	TlsTable,
	LoadConfigurationTable,
	BoundImport,
	ImportAddressTable,
	DelayImportDescriptor,
	ClrRuntimeHeader,
}

[Parser("pe", "可移植可执行文件", [ ".exe", ".dll", ".mui", ".sys", ".scr", ".cpl", ".ocx", ".ax", ".fon", ".efi" ])]
public class PortableExecutableParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	public long CoffHeaderOffset { get; private set; } = 0;
	public long CoffOptionalSectionOffset { get; private set; } = 0;
	public long DataDirectoryTableOffset { get; private set; } = 0;

	public IEnumerable<SubFile> GetSubFiles()
	{
		using BinaryReader br = new(InputStream, Encoding.ASCII, true);

		yield return new("DOS头", 0, 0x40);

		var peDosHdrMagic = br.ReadString(2); // "MZ"
		br.BaseStream.Position = 0x3c;
		CoffHeaderOffset = br.ReadUInt32();
		br.BaseStream.Position = CoffHeaderOffset;

		yield return new("DOS存根", 0x40, CoffHeaderOffset) { IconString = "🔶" };
		yield return new("COFF头", CoffHeaderOffset, 0x18) { IconString = "🔶" };

		// PE COFF Header
		var peMagic = br.ReadString(4); // "PE\0\0"
		var peMachineId = br.ReadUInt16();
		var peSectionCount = br.ReadUInt16();
		var peTimestamp = br.ReadUInt32();
		var peSymTabPtr = br.ReadUInt32(); // unused
		var peSymTabCount = br.ReadUInt32(); // unused
		var peOptionalHeaderSize = br.ReadUInt16();
		var peFlags = br.ReadUInt16();
		Logger.Debug($"PE COFF头：机器码 {peMachineId:X4}，{peSectionCount} 个节区");
		bool is64Bit = peMachineId == 0x8664;
		
		// PE Optional Header
		CoffOptionalSectionOffset = br.BaseStream.Position;
		var peOptHdrMagic = br.ReadUInt16();
		bool isPe32Plus = peOptHdrMagic == 0x020b;
		var peLinkerVerMajor = br.ReadByte();
		var peLinkerVerMinor = br.ReadByte();
		var peSizeOfCode = br.ReadUInt32();
		var peSizeOfInitData = br.ReadUInt32();
		var peSizeOfUninitData = br.ReadUInt32();
		var peEntryPointOfs = br.ReadUInt32();
		var peBaseOfCode = br.ReadUInt32();
		var peBaseOfData = isPe32Plus ? 0 : br.ReadUInt32();
		// NT-specific
		var peNtImageBase = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtSectionAlignment = br.ReadUInt32();
		var peNtFileAlignment = br.ReadUInt32();
		var peNtOsVerMajor = br.ReadUInt16();
		var peNtOsVerMinor = br.ReadUInt16();
		var peNtImageVerMajor = br.ReadUInt16();
		var peNtImageVerMinor = br.ReadUInt16();
		var peNtSubsysVerMajor = br.ReadUInt16();
		var peNtSubsysVerMinor = br.ReadUInt16();
		br.ReadUInt32(); // reserved
		var peNtSizeOfImage = br.ReadUInt32();
		var peNtSizeOfHeaders = br.ReadUInt32();
		var peNtChecksum = br.ReadUInt32();
		var peNtSubsystem = br.ReadUInt16();
		var peNtDllFlags = br.ReadUInt16();
		var peNtSizeOfStackReserve = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtSizeOfStackCommit = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtSizeOfHeapReserve = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtSizeOfHeapCommit = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtLoaderFlags = br.ReadUInt32();
		var peNtRvaSizePairCount = br.ReadUInt32();
		Logger.Debug($"PE可选头：魔数 {peOptHdrMagic:X4}，代码大小 {peSizeOfCode:X8}，入口点 {peEntryPointOfs:X8}");
		Logger.Debug($"NT头：映像基址 {peNtImageBase:X8}，系统版本 {peNtOsVerMajor}.{peNtOsVerMinor}，子系统 {peNtSubsystem}，{peNtRvaSizePairCount} 个目录");
		yield return new("COFF可选头", CoffOptionalSectionOffset, br.BaseStream.Position - CoffOptionalSectionOffset) { IconString = "🔶" };

		// Data Dirs.
		DataDirectoryTableOffset = br.BaseStream.Position;
		Logger.Debug("正在读取PE数据目录…");
		for (int i = 0; i < peNtRvaSizePairCount; i++)
		{
			var dataDirRva = br.ReadUInt32();
			var dataDirSize = br.ReadUInt32();
			if (dataDirRva != 0)
			{
				Logger.Debug($"PE数据目录 {i,2}：RVA {dataDirRva:X16} 大小 {dataDirSize}");
			}
		}
		yield return new("数据目录表", DataDirectoryTableOffset, br.BaseStream.Position - DataDirectoryTableOffset) { IconString = "🔶" };

		// Sections
		Logger.Debug("正在读取PE节区头…");

		for (int i = 0; i < peSectionCount; i++)
		{
			var sectOfs = br.BaseStream.Position;

			var sectName = br.ReadString(8).TrimEnd('\0');
			var sectSize = br.ReadUInt32();
			var sectVirtualAddr = br.ReadUInt32();
			var sectRawDataSize = br.ReadUInt32();
			var sectRawDataPtr = br.ReadUInt32();
			var sectRelocPtr = br.ReadUInt32();
			var sectLineNumPtr = br.ReadUInt32();
			var sectRelocCount = br.ReadUInt16();
			var sectLineNumCount = br.ReadUInt16();
			var sectFlags = br.ReadUInt32();

			Logger.Debug($"PE节区：{sectName} 大小 {sectSize:X8} 虚拟地址 {sectVirtualAddr:X8} 数据 {sectRawDataPtr:X8}:{sectRawDataSize:X8}");
			yield return new(sectName, (long)sectVirtualAddr, (long)sectSize);

			br.BaseStream.Position = sectOfs + 40;
		}
	}
}
