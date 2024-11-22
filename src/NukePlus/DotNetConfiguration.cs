using System.Reflection;
using JetBrains.Annotations;

namespace NukePlus;

[TypeConverter(typeof(TypeConverter<DotNetConfiguration>))]
public class DotNetConfiguration : Enumeration
{
    public DotNetConfiguration(string name) => Value = name;
    public static readonly DotNetConfiguration Debug = new(nameof(Debug));
    public static readonly DotNetConfiguration Release = new(nameof(Release));
    public static implicit operator string(DotNetConfiguration configuration) => configuration.Value;
}

public record Sdk(string Version, string RollForward);

public record GlobalJson(Sdk Sdk);

[PublicAPI, UsedImplicitly(ImplicitUseKindFlags.Assign)]
public class GlobalJsonAttribute : ParameterAttribute
{
    readonly AbsolutePath filePath;

    public GlobalJsonAttribute()
    {
        filePath = NukeBuild.RootDirectory / "global.json";
        Assert.FileExists(filePath);
    }

    public override bool List { get; set; }

    public override object GetValue(MemberInfo member, object instance)
        => filePath.ReadJson<GlobalJson>();
}
