using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace RelicAtlas.UI;

// The same Graphite palette and compact spacing used by Hunt Helper Evolved.
internal sealed class AtlasTheme : IDisposable
{
    private int colors;
    private int vars;
    public static Vector4 Rgb(uint color) => new((color >> 16 & 255) / 255f,(color >> 8 & 255) / 255f,(color & 255) / 255f,1);
    public AtlasTheme()
    {
        foreach (var (slot,color) in new[] {
            (ImGuiCol.Text,0xedf0f2u),(ImGuiCol.TextDisabled,0xb0b8c0u),
            (ImGuiCol.WindowBg,0x181a1cu),(ImGuiCol.ChildBg,0x181a1cu),(ImGuiCol.PopupBg,0x222426u),
            (ImGuiCol.Border,0x41464au),(ImGuiCol.TitleBg,0x2a2d30u),(ImGuiCol.TitleBgActive,0x222426u),
            (ImGuiCol.FrameBg,0x222426u),(ImGuiCol.FrameBgHovered,0x293b58u),(ImGuiCol.FrameBgActive,0x354b6bu),
            (ImGuiCol.Button,0x222426u),(ImGuiCol.ButtonHovered,0x293b58u),(ImGuiCol.ButtonActive,0x354b6bu),
            (ImGuiCol.Header,0x293b58u),(ImGuiCol.HeaderHovered,0x354b6bu),(ImGuiCol.HeaderActive,0x293b58u),
            (ImGuiCol.Tab,0x2a2d30u),(ImGuiCol.TabHovered,0x354b6bu),(ImGuiCol.TabActive,0x293b58u),
            (ImGuiCol.CheckMark,0x81aaffu),(ImGuiCol.PlotHistogram,0x68d4dcu),(ImGuiCol.Separator,0x41464au),
            (ImGuiCol.TableHeaderBg,0x2a2d30u),(ImGuiCol.TableBorderLight,0x41464au),
            (ImGuiCol.ScrollbarBg,0x181a1cu),(ImGuiCol.ScrollbarGrab,0x41464au),
            (ImGuiCol.ScrollbarGrabHovered,0xb0b8c0u),(ImGuiCol.ScrollbarGrabActive,0x81aaffu)
        }) Color(slot,Rgb(color));
        Color(ImGuiCol.TableRowBg,Vector4.Zero);
        Color(ImGuiCol.TableRowBgAlt,new Vector4(1,1,1,.025f));
        var scale = ImGuiHelpers.GlobalScale;
        Var(ImGuiStyleVar.WindowRounding,4 * scale); Var(ImGuiStyleVar.ChildRounding,2 * scale);
        Var(ImGuiStyleVar.FrameRounding,3 * scale); Var(ImGuiStyleVar.PopupRounding,4 * scale);
        Var(ImGuiStyleVar.TabRounding,2 * scale); Var(ImGuiStyleVar.ScrollbarSize,11 * scale);
        Var(ImGuiStyleVar.WindowBorderSize,1); Var(ImGuiStyleVar.ChildBorderSize,1); Var(ImGuiStyleVar.FrameBorderSize,1);
        VectorVar(ImGuiStyleVar.WindowPadding,new Vector2(10,9) * scale);
        VectorVar(ImGuiStyleVar.FramePadding,new Vector2(7,3) * scale);
        VectorVar(ImGuiStyleVar.ItemSpacing,new Vector2(7,5) * scale);
        VectorVar(ImGuiStyleVar.CellPadding,new Vector2(7,4) * scale);
    }
    private void Color(ImGuiCol slot,Vector4 value) { ImGui.PushStyleColor(slot,value); colors++; }
    private void Var(ImGuiStyleVar slot,float value) { ImGui.PushStyleVar(slot,value); vars++; }
    private void VectorVar(ImGuiStyleVar slot,Vector2 value) { ImGui.PushStyleVar(slot,value); vars++; }
    public void Dispose() { ImGui.PopStyleVar(vars); ImGui.PopStyleColor(colors); vars=colors=0; }
}
