using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unai.ExtendedBinaryWaterfall.Exporters;
using Unai.ExtendedBinaryWaterfall.Parsers;

namespace Unai.ExtendedBinaryWaterfall.Cli;

class Program
{
	static bool _helpMode = false;

	static readonly Generator _generator = new();

	static void Main(string[] args)
	{
		// Make decimals use "." instead of other characters.
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

		Logger.Info($"{BuildInfo.ApplicationName} {BuildInfo.SemVer ?? "unknown"}");

		if (args.Length < 1)
		{
			Logger.Error("必须指定至少一个参数。");
			PrintHelp();
			return;
		}

		if (!ParseCommandLineArguments(args))
		{
			Logger.Fail("命令行参数无效。正在退出…");
			return;
		}

		if (_helpMode)
		{
			PrintHelp();
			return;
		}

		try
		{
			_generator.Initialize();
			_generator.Generate();
		}
		catch (Exception ex)
		{
			Logger.Fail($"生成二进制瀑布时未处理的异常：{ex}");
		}
	}

	private static bool ParseCommandLineArguments(IEnumerable<string> args = null)
	{
		Logger.Info("解析命令行参数：");

		foreach (var arg in args ?? Environment.GetCommandLineArgs()[1..])
		{
			Logger.Debug($"解析命令行参数：`{arg}`");

			if (!arg.StartsWith('-'))
			{
				if (_generator.InputFilePath != null)
				{
					Logger.Error("无法指定两个以上的输入文件。");
					return false;
				}
				_generator.InputFilePath = arg;
				continue;
			}

			var argKvp = arg.Split('=');

			if (argKvp[0] == "--help" || argKvp[0] == "-h" || argKvp[0] == "-?")
			{
				_helpMode = true;
				continue;
			}

			var targetParam = Utils.GetPropertyFromCliArgument(argKvp[0]);

			if (targetParam == null)
			{
				Logger.Error($"未知参数：`{argKvp[0]}`");
				return false;
			}

			// Can't do a `switch` statement here. :(
			if (targetParam.DeclaringType == typeof(Generator))
			{
				if (!CliParameterAttribute.SetPropertyFromCliArgument(targetParam, _generator, argKvp[1]))
				{
					return false;
				}
			}
			else if (targetParam.DeclaringType.GetInterfaces().Contains(typeof(IExporter)))
			{
				_generator.AdditionalCliArguments.Add(argKvp[0], argKvp[1]);
				continue;
			}
			else
			{
				Logger.Error($"无法设置属性 `{targetParam.Name}`，因为其声明类型的实例未知。");
				return false;
			}
		}

		return true;
	}

	private static void PrintHelp()
	{
		StringBuilder helpStrBld = new();
		helpStrBld.AppendLine("命令格式：");
		helpStrBld.AppendLine($"	{Path.GetFileName(Environment.GetCommandLineArgs()[0])} <文件输入路径> [选项]");
		helpStrBld.AppendLine();
		helpStrBld.AppendLine("选项：");
		helpStrBld.AppendLine($"	-h, -?, --help\n		打印此帮助文本并退出");

		void AppendCommandLineArgument(PropertyInfo prop, int indentation = 1)
		{
			var cliParamAttr = prop.GetCustomAttribute<CliParameterAttribute>();
			helpStrBld.Append(new string('\t', indentation));
			if (cliParamAttr.ShortParameterName.HasValue)
			{
				helpStrBld.Append($"-{cliParamAttr.ShortParameterName}, ");
			}
			helpStrBld.Append($"--{cliParamAttr.LongParameterName}=<{prop.PropertyType.Name}> ".PadRight(cliParamAttr.ShortParameterName.HasValue ? 28 : 32));
			helpStrBld.AppendLine(cliParamAttr.Name);
			if (cliParamAttr.Description != null)
			{
				helpStrBld.AppendLine($"{new string('\t', indentation + 1)}{cliParamAttr.Description}");
			}
		}

		foreach (var prop in Utils.GetPropertiesWithAttribute<CliParameterAttribute>(typeof(Generator)))
		{
			AppendCommandLineArgument(prop);
		}
		helpStrBld.AppendLine();

		helpStrBld.AppendLine("可用的解析器/输入格式：");
		foreach (var parserKvp in Utils.GetTypesWithAttribute<ParserAttribute>())
		{
			var parserAttr = parserKvp.Key;
			helpStrBld.AppendLine($"	{parserAttr.Id.PadRight(16)} {parserAttr.Name}");
		}
		helpStrBld.AppendLine();

		helpStrBld.AppendLine("可用的导出路径：");
		foreach (var exporterKvp in Utils.GetTypesWithAttribute<ExporterAttribute>())
		{
			var exporterAttr = exporterKvp.Key;
			helpStrBld.AppendLine($"	{exporterAttr.Id.PadRight(16)} {exporterAttr.Name} – {exporterAttr.Description}");

			var cliParams = Utils.GetPropertiesWithAttribute<CliParameterAttribute>(exporterKvp.Value).ToList();
			if (cliParams.Count > 0)
			{
				helpStrBld.AppendLine($"		选项：");
				foreach (var cliParam in cliParams)
				{
					AppendCommandLineArgument(cliParam, 3);
				}
				helpStrBld.AppendLine();
			}
		}

		Console.Error.WriteLine(helpStrBld);
	}
}
