using System;
using System.Diagnostics;
using System.Reflection;

namespace Unai.ExtendedBinaryWaterfall;

public static class BuildInfo
{
	public static string ApplicationName { get; } = "Extended Binary Waterfall（扩展二进制瀑布图）";

	public static string FullSemVer { get; private set; } = null;
	public static string SemVer { get; private set; } = null;
	public static string GitCommit { get; private set; } = null;

	static BuildInfo()
	{
		try
		{
			var asmInfVerAttr = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			if (asmInfVerAttr != null)
			{
				FullSemVer = asmInfVerAttr.InformationalVersion;
			}
			else
			{
				FullSemVer = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
			}

			if (FullSemVer != null)
			{
				if (FullSemVer.Contains('+'))
				{
					SemVer = FullSemVer[..FullSemVer.IndexOf('+')];
					GitCommit = FullSemVer[(FullSemVer.IndexOf('+')+1)..];
				}
				else
				{
					SemVer = FullSemVer;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error($"无法获取或计算程序版本：{ex.Message}");
		}
	}
}