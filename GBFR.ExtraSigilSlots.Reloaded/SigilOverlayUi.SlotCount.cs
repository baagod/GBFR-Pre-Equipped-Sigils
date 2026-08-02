using DearImguiSharp;
using System.Globalization;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal sealed unsafe partial class SigilOverlayUi
{
    private const int ImGuiInputTextFlagsEnterReturnsTrue = 1 << 5;

    private readonly byte[] _virtualSlotCountBuffer = new byte[16];
    private int _pendingVirtualSlotCount;
    private int _slotCountConfirmationTarget;
    private bool _virtualSlotCountUiInitialized;
    private bool _openSlotCountConfirmationNextFrame;
    private bool _slotCountConfirmationOpen = true;
    private string _slotCountStatus = string.Empty;
    private bool _slotCountStatusIsError;

    private void InitializeVirtualSlotCountUi()
    {
        int pending = NativeCore.GetPendingVirtualSlotCount();
        _pendingVirtualSlotCount = pending is >= 1 and <= NativeCore.VirtualSlotCapacity
            ? pending
            : 0;
        int displayed = _pendingVirtualSlotCount != 0
            ? _pendingVirtualSlotCount
            : ActiveVirtualSlotCount;
        SetUtf8BufferText(
            _virtualSlotCountBuffer,
            displayed.ToString(CultureInfo.InvariantCulture));
        _slotCountStatus = string.Empty;
        _slotCountStatusIsError = false;
        _virtualSlotCountUiInitialized = true;
    }

    private void DrawVirtualSlotCountSetting(bool english)
    {
        if (!_virtualSlotCountUiInitialized)
            InitializeVirtualSlotCountUi();

        ImGui.Separator();
        ImGui.Text(
            english
                ? $"Current effective extra slots: {ActiveVirtualSlotCount}"
                : $"当前生效的扩展因子槽：{ActiveVirtualSlotCount}");
        if (_pendingVirtualSlotCount != 0)
        {
            ImGui.TextColored(
                _readOnlyColor,
                english
                    ? $"Pending for next restart: {_pendingVirtualSlotCount}"
                    : $"下次重启待生效：{_pendingVirtualSlotCount}");
        }
        ImGui.Text(
            english
                ? $"Set the number of extra slots (1-{NativeCore.VirtualSlotCapacity}, restart required; maximum 24)."
                : $"设置扩展因子槽数量（1-{NativeCore.VirtualSlotCapacity}，重启后生效，最多扩展至 24 个）。");

        ImGui.SetNextItemWidth(100.0f);
        bool enterPressed;
        fixed (byte* slotCountBuffer = _virtualSlotCountBuffer)
        {
            enterPressed = ImGui.InputTextWithHint(
                "##virtual_slot_count",
                "1-24",
                (sbyte*)slotCountBuffer,
                (IntPtr)_virtualSlotCountBuffer.Length,
                ImGuiInputTextFlagsEnterReturnsTrue,
                null!,
                IntPtr.Zero);
        }
        ImGui.SameLine(0.0f, -1.0f);
        bool savePressed = ImGui.Button(
            english ? "Save for restart##slot_count" : "保存（重启后生效）##slot_count",
            _zeroSize);
        if (enterPressed || savePressed)
            CommitVirtualSlotCountInput();

        if (_slotCountStatus.Length != 0)
        {
            ImGui.TextColored(
                _slotCountStatusIsError ? _conflictColor : _successColor,
                _slotCountStatus);
        }
    }

    private void CommitVirtualSlotCountInput()
    {
        string input = GetUtf8BufferText(_virtualSlotCountBuffer);
        int target = VirtualSlotCountInput.Normalize(
            input,
            NativeCore.VirtualSlotCapacity);
        SetUtf8BufferText(
            _virtualSlotCountBuffer,
            target.ToString(CultureInfo.InvariantCulture));

        if (target < ActiveVirtualSlotCount)
        {
            _slotCountConfirmationTarget = target;
            _slotCountConfirmationOpen = true;
            _openSlotCountConfirmationNextFrame = true;
            return;
        }
        SavePendingVirtualSlotCount(target);
    }

    private void DrawVirtualSlotCountConfirmation(bool english)
    {
        string title = english
            ? "Confirm extra-slot reduction##GBFRESSlotCount"
            : "确认缩减扩展因子槽##GBFRESSlotCount";
        if (_openSlotCountConfirmationNextFrame)
        {
            _openSlotCountConfirmationNextFrame = false;
            ImGui.OpenPopupStr(title, 0);
        }
        ImGui.SetNextWindowSize(_dialogSize, 1 << 3);
        if (!ImGui.BeginPopupModal(
                title,
                ref _slotCountConfirmationOpen,
                ImGuiWindowFlagsNoSavedSettings))
            return;

        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        ImGui.Text(
            english
                ? $"Reduce extra slots from {ActiveVirtualSlotCount} to {_slotCountConfirmationTarget}?"
                : $"将扩展因子槽从 {ActiveVirtualSlotCount} 个缩减到 {_slotCountConfirmationTarget} 个？");
        ImGui.TextWrapped(
            english
                ? "After the next restart, the removed slots will be cleared for every character and their sigils will become available again. No inventory sigil will be deleted. Saved presets retain all 24 slot definitions."
                : "下次重启后，所有角色超出新上限的当前扩展槽都会被清空，相关因子会重新变为可用。库存因子不会被删除，已保存预设仍会保留全部 24 个槽位定义。");
        if (ImGui.Button(english ? "Confirm" : "确认", _zeroSize))
        {
            SavePendingVirtualSlotCount(_slotCountConfirmationTarget);
            ImGui.CloseCurrentPopup();
            _slotCountConfirmationOpen = false;
        }
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Cancel" : "取消", _zeroSize))
        {
            ImGui.CloseCurrentPopup();
            _slotCountConfirmationOpen = false;
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private void SavePendingVirtualSlotCount(int target)
    {
        NativeCore.VirtualSlotCountRequestResult result =
            NativeCore.RequestVirtualSlotCount(target);
        switch (result)
        {
            case NativeCore.VirtualSlotCountRequestResult.Pending:
                _pendingVirtualSlotCount = target;
                _slotCountStatus = UiLocalization.IsEnglish(_state.Language)
                    ? $"Saved. {target} extra slots will take effect after the next restart."
                    : $"已保存。下次重启后将启用 {target} 个扩展因子槽。";
                _slotCountStatusIsError = false;
                break;
            case NativeCore.VirtualSlotCountRequestResult.Cleared:
                _pendingVirtualSlotCount = 0;
                _slotCountStatus = UiLocalization.IsEnglish(_state.Language)
                    ? "The pending slot-count change was cancelled."
                    : "已取消待生效的槽位数量修改。";
                _slotCountStatusIsError = false;
                break;
            default:
                _slotCountStatus = UiLocalization.IsEnglish(_state.Language)
                    ? "Could not save the slot-count change. The current configuration was not changed."
                    : "无法保存槽位数量修改，当前配置未发生变化。";
                _slotCountStatusIsError = true;
                break;
        }
    }
}
