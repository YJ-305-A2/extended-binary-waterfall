using System;
using System.Reflection;

namespace Unai.ExtendedBinaryWaterfall;

[AttributeUsage(AttributeTargets.Property)]
public class CliParameterAttribute : Attribute
{
	public string LongParameterName { get; set; } = null;
	public char? ShortParameterName { get; set; } = null;
	public string Name { get; set; } = null;
	public string Description { get; set; } = null;

	public CliParameterAttribute(string name, string longParamName, char shortParamName, string desc = null)
	{
		Name = name;
		LongParameterName = longParamName;
		ShortParameterName = shortParamName;
		Description = desc;
	}

	public CliParameterAttribute(string name, string longParamName, string desc = null)
	{
		Name = name;
		LongParameterName = longParamName;
		Description = desc;
	}

	public CliParameterAttribute() {}

	public static bool SetPropertyFromCliArgument(PropertyInfo targetProp, object targetObject, string value)
	{
		if (targetProp.PropertyType == typeof(string))
		{
			targetProp.SetValue(targetObject, value.Replace("\\n", "\n"));
		}
		else if (targetProp.PropertyType == typeof(int))
		{
			targetProp.SetValue(targetObject, int.Parse(value));
		}
		else if (targetProp.PropertyType.IsEnum)
		{
			var ok = Enum.TryParse(targetProp.PropertyType, value, true, out var pval);
			if (!ok)
			{
				Logger.Error($"无法将值 '{value}' 解析为枚举类型 '{targetProp.PropertyType.Name}'。");
				Logger.Info("有效值：");
				foreach (var enumVal in Enum.GetValues(targetProp.PropertyType))
				{
					Logger.Info($"	{enumVal}");
				}
				return false;
			}
			targetProp.SetValue(targetObject, pval);
		}
		else if (targetProp.PropertyType == typeof(bool))
		{
			targetProp.SetValue(targetObject, bool.Parse(value));
		}
		else
		{
			Logger.Error($"无法转换属性 `{targetProp.Name}` 值的字符串表示形式，因为该功能尚未实现。");
		}
		return true;
	}
}
