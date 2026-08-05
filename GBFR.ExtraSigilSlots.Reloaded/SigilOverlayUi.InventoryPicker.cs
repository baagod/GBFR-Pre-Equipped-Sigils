using DearImguiSharp;
using System.Text;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal sealed unsafe partial class SigilOverlayUi
{
    private void DrawUsageFilterButtons(bool english)
    {
        ImGui.Text(english ? "Filter" : "搜索条件");
        ImGui.SameLine(0.0f, -1.0f);
        DrawUsageFilterButton(InventoryUsageFilter.All, english ? "All" : "所有因子");
        ImGui.SameLine(0.0f, -1.0f);
        DrawUsageFilterButton(InventoryUsageFilter.Used, english ? "Used" : "已使用的因子");
        ImGui.SameLine(0.0f, -1.0f);
        DrawUsageFilterButton(
            InventoryUsageFilter.BodyUsed,
            english ? "Body used" : "本体已使用的因子");
        ImGui.SameLine(0.0f, -1.0f);
        DrawUsageFilterButton(
            InventoryUsageFilter.ExtensionUsed,
            english ? "Extension used" : "扩展已使用的因子");
        ImGui.SameLine(0.0f, -1.0f);
        DrawUsageFilterButton(
            InventoryUsageFilter.Unused,
            english ? "Unused" : "未被使用的因子");
    }

    private void DrawUsageFilterButton(InventoryUsageFilter filter, string label)
    {
        ImGui.BeginDisabled(_usageFilter == filter);
        if (ImGui.SmallButton($"{label}##usage_{filter}"))
            _usageFilter = filter;
        ImGui.EndDisabled();
    }

    private string BuildInventoryDisplayLabel(
        NativeCore.InventoryView item,
        uint currentCharacterHash,
        bool english)
    {
        List<string> details = [];
        if (item.Equipped)
        {
            string owner = UiLocalization.CharacterName(item.Gem.WornBy, english);
            details.Add(item.Gem.WornBy == currentCharacterHash
                ? english ? $"Current character: {owner}" : $"当前角色：{owner}"
                : owner);
            details.Add(english ? "body used" : "本体已使用");
        }
        else if (item.VirtualOwnerCharacterHash != 0)
        {
            string owner = UiLocalization.CharacterName(item.VirtualOwnerCharacterHash, english);
            details.Add(item.VirtualOwnerCharacterHash == currentCharacterHash
                ? english ? $"Current character: {owner}" : $"当前角色：{owner}"
                : owner);
            details.Add(english
                ? $"extension slot {NativeSlotCount + item.VirtualOwnerSlot}"
                : $"扩展已使用（槽 {NativeSlotCount + item.VirtualOwnerSlot}）");
        }

        IReadOnlyList<string> presetNames =
            _presetStore.GetPresetNamesForSlot(item.Gem.SlotId);
        if (presetNames.Count != 0)
            details.Add((english ? "Presets: " : "预设：") + string.Join("、", presetNames));
        if (details.Count == 0)
            return item.Label;
        string separator = english ? " | " : "｜";
        return $"[{string.Join(separator, details)}] {item.Label}";
    }

    private void HandleInventoryItemClick(
        NativeCore.InventoryView item,
        uint characterHash,
        bool english)
    {
        if (item.Equipped)
        {
            _pendingBodyItem = item;
            _bodyDialogOpen = true;
            _requestBodyDialogOpen = true;
            return;
        }

        bool exactCurrentSlot = item.VirtualOwnerCharacterHash == characterHash &&
            item.VirtualOwnerSlot == _pickerSlot;
        if (exactCurrentSlot)
        {
            ClosePickerPopup();
            return;
        }
        if (item.VirtualOwnerCharacterHash != 0 && !_suppressTransferPrompt)
        {
            _pendingTransferItem = item;
            _transferDialogOpen = true;
            _requestTransferDialogOpen = true;
            return;
        }

        bool clearPresetReferences = item.VirtualOwnerCharacterHash != 0;
        if (ApplyInventorySelection(item, characterHash, clearPresetReferences, english))
            ClosePickerPopup();
    }

    private bool DrawInventoryConflictDialogs(uint characterHash, bool english)
    {
        DrawBodyBlockedDialog(english);
        return DrawTransferDialog(characterHash, english);
    }

    private void OpenRequestedInventoryConflictDialogs(bool english)
    {
        if (_requestBodyDialogOpen)
        {
            _requestBodyDialogOpen = false;
            ImGui.OpenPopupStr(BodyBlockedTitle(english), 0);
        }
        if (_requestTransferDialogOpen)
        {
            _requestTransferDialogOpen = false;
            ImGui.OpenPopupStr(TransferTitle(english), 0);
        }
    }

    private void DrawBodyBlockedDialog(bool english)
    {
        if (_pendingBodyItem is null)
            return;
        ImGui.SetNextWindowSize(_dialogSize, 1 << 3);
        if (!ImGui.BeginPopupModal(
                BodyBlockedTitle(english),
                ref _bodyDialogOpen,
                ImGuiWindowFlagsNoSavedSettings))
        {
            if (!_bodyDialogOpen)
                _pendingBodyItem = null;
            return;
        }

        string owner = UiLocalization.CharacterName(_pendingBodyItem.Gem.WornBy, english);
        ImGui.Text(english
            ? $"This sigil is already used in {owner}'s body slots."
            : $"当前因子已被{owner}的本体因子栏使用。");
        ImGui.Text(english
            ? "To put it in a virtual extension slot, remove it from that character first,"
            : "若你想将其放入虚拟扩展栏，请先到对应角色位置脱除因子，");
        ImGui.Text(english ? "then add it again." : "然后再重新添加。");
        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        if (ImGui.Button(english ? "OK" : "知道了", _zeroSize))
        {
            _pendingBodyItem = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private bool DrawTransferDialog(uint characterHash, bool english)
    {
        if (_pendingTransferItem is null)
            return false;
        bool closePicker = false;
        ImGui.SetNextWindowSize(_dialogSize, 1 << 3);
        if (!ImGui.BeginPopupModal(
                TransferTitle(english),
                ref _transferDialogOpen,
                ImGuiWindowFlagsNoSavedSettings))
        {
            if (!_transferDialogOpen)
                _pendingTransferItem = null;
            return false;
        }

        NativeCore.InventoryView item = _pendingTransferItem;
        string owner = UiLocalization.CharacterName(item.VirtualOwnerCharacterHash, english);
        int sourceSlot = NativeSlotCount + item.VirtualOwnerSlot;
        ImGui.Text(english
            ? $"The selected sigil is used by {owner} in extension slot {sourceSlot}."
            : $"当前因子已被{owner}用于虚拟扩展槽 {sourceSlot}。");

        IReadOnlyList<string> presetNames =
            _presetStore.GetPresetNamesForSlot(
                item.VirtualOwnerCharacterHash,
                item.Gem.SlotId);
        if (presetNames.Count != 0)
        {
            ImGui.Text((english ? "Referenced by presets: " : "同时被以下预设引用：") +
                string.Join("、", presetNames));
        }
        ImGui.Text(english
            ? $"Confirming moves it to {UiLocalization.CharacterName(characterHash, true)}."
            : $"确认后会将其转移给{UiLocalization.CharacterName(characterHash, false)}。");
        ImGui.Text(english
            ? $"{owner}'s current extension slot will be cleared."
            : $"{owner}的当前虚拟因子槽将直接清空。");
        ImGui.Text(english
            ? "Every related preset position will also be cleared."
            : "所有相关预设中的对应位置也会直接清空。");

        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        ImGui.Checkbox(
            english ? "Do not ask again while this menu is open"
                    : "当前菜单期间不再提示",
            ref _suppressTransferPrompt);
        if (ImGui.Button(english ? "No" : "否", _zeroSize))
        {
            _pendingTransferItem = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Yes" : "是", _zeroSize))
        {
            if (ApplyInventorySelection(item, characterHash, true, english))
            {
                _pendingTransferItem = null;
                ImGui.CloseCurrentPopup();
                closePicker = true;
            }
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
        return closePicker;
    }

    private bool ApplyInventorySelection(
        NativeCore.InventoryView item,
        uint characterHash,
        bool clearPresetReferences,
        bool english)
    {
        if (_pickerSlot < 0 || _pickerSlot >= ActiveVirtualSlotCount)
            return false;
        int targetSlot = _pickerSlot;
        uint sourceCharacterHash = item.VirtualOwnerCharacterHash;
        try
        {
            IReadOnlyList<string> affectedPresets = Array.Empty<string>();
            bool success = clearPresetReferences
                ? _presetStore.ClearSlotReferencesAndRun(
                    sourceCharacterHash,
                    item.Gem.SlotId,
                    () => NativeCore.SetSelection(
                        characterHash,
                        targetSlot,
                        item.Gem.SlotId),
                    out affectedPresets)
                : NativeCore.SetSelection(characterHash, targetSlot, item.Gem.SlotId);
            if (!success)
            {
                SetPresetStatus(
                    english ? "The sigil could not be assigned." : "无法分配当前因子。",
                    true);
                return false;
            }

            _presetConflicts.Remove((characterHash, targetSlot));
            if (sourceCharacterHash != 0)
                _presetConflicts.Remove((sourceCharacterHash, item.VirtualOwnerSlot));
            MarkPresetTemporary(characterHash);
            if (sourceCharacterHash != 0 && sourceCharacterHash != characterHash)
                MarkPresetTemporary(sourceCharacterHash);
            LoadSelection(characterHash);
            RefreshInventory();
            if (affectedPresets.Count != 0)
            {
                SetPresetStatus(
                    english
                        ? $"Moved the sigil and cleared presets: {string.Join(", ", affectedPresets)}"
                        : $"因子已转移，并清空相关预设：{string.Join("、", affectedPresets)}",
                    false);
            }
            return true;
        }
        catch (Exception exception)
        {
            _log($"Sigil transfer failed: {exception}");
            SetPresetStatus(
                english ? "The sigil transfer failed." : "转移因子失败。",
                true);
            return false;
        }
    }

    private void ClearVirtualSlot(uint characterHash, int slot)
    {
        if (slot < 0 || slot >= ActiveVirtualSlotCount)
            return;
        if (NativeCore.SetSelection(characterHash, slot, 0))
        {
            _presetConflicts.Remove((characterHash, slot));
            MarkPresetTemporary(characterHash);
            LoadSelection(characterHash);
            RefreshInventory();
        }
    }

    private void ClosePickerPopup()
    {
        _pickerOpen = false;
        _pickerSlot = -1;
        _pendingBodyItem = null;
        _pendingTransferItem = null;
        _requestBodyDialogOpen = false;
        _requestTransferDialogOpen = false;
        ImGui.CloseCurrentPopup();
    }

    private string PresetConflictText(NativeCore.PresetSlotResult conflict, bool english)
    {
        if (conflict.Status == NativeCore.PresetSlotStatus.Equipped)
        {
            string owner = UiLocalization.CharacterName(conflict.OwnerCharacterHash, english);
            return english
                ? $"This sigil is used by {owner}; please choose another."
                : $"当前因子被用在{owner}角色上，请您替换另一个因子";
        }
        return english
            ? "This preset sigil is unavailable; please choose another."
            : "当前预设因子不可用，请您替换另一个因子";
    }

    private void SetPresetStatus(string text, bool isError)
    {
        _presetStatus = text;
        _presetStatusIsError = isError;
    }

    private static string GetUtf8BufferText(byte[] buffer)
    {
        int length = Array.IndexOf(buffer, (byte)0);
        if (length < 0)
            length = buffer.Length;
        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    private static void SetUtf8BufferText(byte[] buffer, string text)
    {
        Array.Clear(buffer);
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        int length = Math.Min(bytes.Length, buffer.Length - 1);
        Array.Copy(bytes, buffer, length);
    }

    private static string PresetManagerTitle(bool english) => english
        ? "Manage presets##GBFRESPresetManager"
        : "管理预设##GBFRESPresetManager";

    private static string PresetNameTitle(bool english) => english
        ? "Preset name##GBFRESPresetName"
        : "预设名称##GBFRESPresetName";

    private static string BodyBlockedTitle(bool english) => english
        ? "Body-used sigil##GBFRESBodyBlocked"
        : "本体已使用的因子##GBFRESBodyBlocked";

    private static string TransferTitle(bool english) => english
        ? "Transfer extension sigil##GBFRESTransfer"
        : "转移扩展因子##GBFRESTransfer";

    private void DisposePresetUiResources()
    {
        _presetManagerSize.Dispose();
        _presetManagerCharacterListSize.Dispose();
        _presetManagerPresetListSize.Dispose();
        _presetTransferDialogSize.Dispose();
        _presetTransferCharacterListSize.Dispose();
        _dialogSize.Dispose();
        _conflictColor.Dispose();
    }
}
