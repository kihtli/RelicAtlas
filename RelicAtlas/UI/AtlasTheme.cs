using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace RelicAtlas.UI;

// Window-local Solution Nine-inspired violet and neon cyan palette for the Atlas canvas.
internal sealed class AtlasTheme : IDisposable
{
    private int colors;
    private int vars;
    public AtlasTheme()
    {
        Color(ImGuiCol.Text, new(.91f,.91f,.95f,1));
        Color(ImGuiCol.TextDisabled, new(.57f,.57f,.66f,1));
        Color(ImGuiCol.WindowBg, new(.04f,.046f,.078f,1));
        Color(ImGuiCol.ChildBg, new(.055f,.061f,.10f,1));
        Color(ImGuiCol.PopupBg, new(.095f,.07f,.15f,1));
        Color(ImGuiCol.Border, new(.29f,.23f,.40f,0));
        Color(ImGuiCol.TitleBg, new(.055f,.061f,.10f,1));
        Color(ImGuiCol.TitleBgActive, new(.115f,.075f,.19f,1));
        Color(ImGuiCol.FrameBg, new(.10f,.105f,.16f,1));
        Color(ImGuiCol.FrameBgHovered, new(.23f,.16f,.33f,1));
        Color(ImGuiCol.FrameBgActive, new(.29f,.20f,.42f,1));
        Color(ImGuiCol.Button, new(.20f,.17f,.31f,1));
        Color(ImGuiCol.ButtonHovered, new(.28f,.19f,.40f,1));
        Color(ImGuiCol.ButtonActive, new(.34f,.23f,.48f,1));
        Color(ImGuiCol.Header, new(.16f,.14f,.25f,1));
        Color(ImGuiCol.HeaderHovered, new(.25f,.17f,.37f,1));
        Color(ImGuiCol.HeaderActive, new(.29f,.20f,.42f,1));
        Color(ImGuiCol.Tab, new(.09f,.06f,.15f,1));
        Color(ImGuiCol.TabHovered, new(.26f,.17f,.39f,1));
        Color(ImGuiCol.TabActive, new(.22f,.14f,.34f,1));
        Color(ImGuiCol.CheckMark, new(.36f,.86f,.94f,1));
        Color(ImGuiCol.PlotHistogram, new(.36f,.86f,.94f,1));
        Color(ImGuiCol.Separator, new(.29f,.23f,.40f,.7f));
        Color(ImGuiCol.TableHeaderBg, new(.10f,.09f,.16f,1));
        Color(ImGuiCol.TableRowBg, new(0,0,0,0));
        Color(ImGuiCol.TableRowBgAlt, new(.7f,.5f,.95f,.035f));
        Color(ImGuiCol.TableBorderLight, new(.26f,.20f,.36f,.35f));
        Color(ImGuiCol.ScrollbarBg, new(.045f,.03f,.075f,.5f));
        Color(ImGuiCol.ScrollbarGrab, new(.25f,.18f,.36f,1));
        Color(ImGuiCol.ScrollbarGrabHovered, new(.38f,.27f,.52f,1));
        Color(ImGuiCol.ScrollbarGrabActive, new(.51f,.37f,.67f,1));
        var scale = ImGuiHelpers.GlobalScale;
        Var(ImGuiStyleVar.WindowRounding, 2 * scale);
        Var(ImGuiStyleVar.ChildRounding, 12 * scale);
        Var(ImGuiStyleVar.FrameRounding, 5 * scale);
        Var(ImGuiStyleVar.PopupRounding, 7 * scale);
        Var(ImGuiStyleVar.TabRounding, 1 * scale);
        Var(ImGuiStyleVar.ScrollbarSize, 10 * scale);
        VectorVar(ImGuiStyleVar.WindowPadding, new Vector2(16,14) * scale);
        VectorVar(ImGuiStyleVar.FramePadding, new Vector2(9,5) * scale);
        VectorVar(ImGuiStyleVar.ItemSpacing, new Vector2(10,9) * scale);
        VectorVar(ImGuiStyleVar.CellPadding, new Vector2(8,9) * scale);
    }
    private void Color(ImGuiCol slot, Vector4 value) { ImGui.PushStyleColor(slot, value); colors++; }
    private void Var(ImGuiStyleVar slot, float value) { ImGui.PushStyleVar(slot, value); vars++; }
    private void VectorVar(ImGuiStyleVar slot, Vector2 value) { ImGui.PushStyleVar(slot, value); vars++; }
    public void Dispose() { ImGui.PopStyleVar(vars); ImGui.PopStyleColor(colors); vars=colors=0; }
}
