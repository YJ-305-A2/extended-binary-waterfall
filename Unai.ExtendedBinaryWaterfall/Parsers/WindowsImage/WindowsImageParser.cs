using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Unai.ExtendedBinaryWaterfall.Parsers.WindowsImage;

[Parser("wim", "Windows映像（WIM）", [ ".wim" ])]
public class WindowsImageParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	public IEnumerable<SubFile> GetSubFiles()
	{
		var ret = ParseWimDir();
		ret = [.. ret
			.Where(sf => sf.Length > 0)
			.GroupBy(sf => sf.StartOffset)
			.Select(sfg => sfg.FirstOrDefault())
			.OrderBy(sf => sf.StartOffset)];
		return
		[
			.. ret,
			new("WIM文件表", ret.OrderBy(sf => sf.EndOffset).FirstOrDefault().EndOffset, InputStream.Length) { IconString = "🔶" },
		];
	}

	private IEnumerable<SubFile> ParseWimDir()
	{
		using var sr = new StreamReader(AuxiliaryInputStream);
		string line;

		bool firstLine = true;
		string filePath = null;
		long fileSize = 0;
		long fileOffset = 0;
		int fileAttrFlags = 0;

		while ((line = sr.ReadLine()) != null)
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
			else if (line.StartsWith("未压缩大小"))
			{
				fileSize = long.Parse(kvp[1].Split(' ')[0]);
			}
			else if (line.StartsWith("在WIM中的偏移"))
			{
				fileOffset = long.Parse(kvp[1].Split(' ')[0]);
			}
			else if (line.StartsWith("属性"))
			{
				fileAttrFlags = int.Parse(kvp[1][2..], System.Globalization.NumberStyles.HexNumber);
			}
		}
		
		yield return new(filePath, fileOffset, fileSize) { IsDirectory = (fileAttrFlags & 0x10) == 0x10 };
	}
}