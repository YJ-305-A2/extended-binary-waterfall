using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Unai.ExtendedBinaryWaterfall.Parsers.WindowsImage;

[Parser("wim", "Windows映像（WIM）", [ ".wim" ])]
public class WindowsImageParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	private StreamReader _auxInputSr = null;

	public IEnumerable<SubFile> GetSubFiles()
	{
		var ret = ParseWimDir();
		ret = [.. ret
			.Where(sf => sf.Length > 0)
			.GroupBy(sf => sf.StartOffset)
			.Select(sfg => sfg.FirstOrDefault())
			.OrderBy(sf => sf.StartOffset)];

		long lastFileEndOfs = 0;

		foreach (var sf in ret)
		{
			yield return sf;
			if (sf.EndOffset > lastFileEndOfs) lastFileEndOfs = sf.EndOffset;
		}

		yield return new("WIM文件表", lastFileEndOfs, InputStream.Length - lastFileEndOfs) { IconString = "🔶" };
	}

	private IEnumerable<SubFile> ParseWimDir()
	{
		if (AuxiliaryInputStream == null) throw new InvalidOperationException("WIM解析器需要存储在由 wimdir 生成的单独文本文件中的文件列表。");
		if (!AuxiliaryInputStream.CanRead) throw new InvalidOperationException("WIM文件列表不可读。");

		_auxInputSr ??= new StreamReader(AuxiliaryInputStream);

		bool firstLine = true;
		string filePath = null;
		long fileSize = 0;
		long fileOffset = 0;
		int fileAttrFlags = 0;

		string line;
		while ((line = _auxInputSr.ReadLine()) != null)
		{
			if (line.StartsWith("--------"))
			{
				if (!firstLine)
				{
					yield return new(filePath, fileOffset, fileSize) { IsDirectory = (fileAttrFlags & 0x10) == 0x10 };
				}
				firstLine = false;
			}
			string[] kvp = line.Split(" = ", 2);
			if (line.StartsWith("完整路径"))
			{
				filePath = kvp[1][1..^1];
			}
			else if (line.StartsWith("压缩大小"))
			{
				fileSize = long.Parse(kvp[1].Split(' ')[0]);
			}
			else if (line.StartsWith("在WIM中的偏移") || line.StartsWith("固体偏移"))
			{
				fileOffset = long.Parse(kvp[1].Split(' ')[0]);
			}
			else if (line.StartsWith("属性"))
			{
				fileAttrFlags = int.Parse(kvp[1][2..], System.Globalization.NumberStyles.HexNumber);
			}
		}

		_auxInputSr.Close();

		yield return new(filePath, fileOffset, fileSize) { IsDirectory = (fileAttrFlags & 0x10) == 0x10 };
	}
}