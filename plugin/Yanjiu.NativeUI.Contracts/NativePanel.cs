// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu.NativeUI;

/// <summary>Optional native UI contract. Callbacks run on the game framework thread.</summary>
public interface INativeModuleUI
{
    NativePanel? CreateNativePanel();
    // Return true when the module has opened its own complete native settings window.
    bool TryOpenNativeSettings() => false;
}

public sealed record NativePanel(string Id,string Title,IReadOnlyList<NativeControl> Controls)
{
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Id)||Id.Length>100)throw new ArgumentException("面板标识无效。");
        if(string.IsNullOrWhiteSpace(Title))throw new ArgumentException("面板标题为空。");
        if(Controls is null||Controls.Count>1000||Controls.Any(control=>control is null))throw new ArgumentException("面板控件数量或内容无效。");
        foreach(var control in Controls){
            if(control.Label is null)throw new ArgumentException("控件标题为空。");
            bool supported=control switch{
                NativeLabel=>true,
                NativeButton button=>button.Click!=null,
                NativeToggle toggle=>toggle.Read!=null&&toggle.Write!=null,
                NativeText text=>text.Read!=null&&text.Write!=null,
                NativeNumber number=>number.Read!=null&&number.Write!=null,
                NativeColor color=>color.Read!=null&&color.Write!=null,
                NativeSelect select=>select.Options!=null&&select.Read!=null&&select.Write!=null,
                _=>false
            };
            if(!supported)throw new ArgumentException("控件类型或回调无效。");
        }
        foreach(var number in Controls.OfType<NativeNumber>())
            if(!double.IsFinite(number.Minimum)||!double.IsFinite(number.Maximum)||number.Minimum>number.Maximum)throw new ArgumentException("数值控件范围无效。");
    }
}

public abstract record NativeControl(string Label)
{
    public Func<bool>? Enabled { get; init; }
}
public sealed record NativeLabel(string Label,Func<string>? Value=null):NativeControl(Label);
public sealed record NativeButton(string Label,Action Click):NativeControl(Label);
public sealed record NativeToggle(string Label,Func<bool> Read,Action<bool> Write):NativeControl(Label);
public sealed record NativeText(string Label,Func<string> Read,Action<string> Write):NativeControl(Label);
public sealed record NativeNumber(string Label,Func<double> Read,Action<double> Write,double Minimum,double Maximum):NativeControl(Label);

public sealed record NativeOption(string Id,string Label);
public sealed record NativeSelect(string Label,Func<IReadOnlyList<NativeOption>> Options,Func<string> Read,Action<string> Write):NativeControl(Label);

public sealed record NativeColor(string Label,Func<System.Numerics.Vector4> Read,Action<System.Numerics.Vector4> Write):NativeControl(Label);
