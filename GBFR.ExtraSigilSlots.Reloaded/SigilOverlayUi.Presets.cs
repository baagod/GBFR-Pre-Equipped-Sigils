using DearImguiSharp;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal sealed unsafe partial class SigilOverlayUi
{
    private enum InventoryUsageFilter
    {
        All,
        Used,
        BodyUsed,
        ExtensionUsed,
        Unused,
    }

    private enum PresetNameMode
    {
        None,
        Create,
        Rename,
    }

    private readonly SigilPresetStore _presetStore;
    private readonly byte[] _presetNameBuffer = new byte[256];
    private readonly ImVec2 _presetManagerSize = MakeVec2(620.0f, 480.0f);
    private readonly ImVec2 _presetManagerChildSize = MakeVec2(0.0f, -82.0f);
    private readonly ImVec2 _dialogSize = MakeVec2(650.0f, 0.0f);
    private readonly ImVec4 _conflictColor = MakeVec4(1.0f, 0.35f, 0.3f, 1.0f);

    private readonly Dictionary<(uint CharacterHash, int Slot),
        NativeCore.PresetSlotResult> _presetConflicts = [];
    private InventoryUsageFilter _usageFilter = InventoryUsageFilter.Unused;
    private string? _selectedPresetId;
    private string _presetStatus = string.Empty;
    private bool _presetStatusIsError;
    private bool _presetManagerOpen = true;
    private bool _openPresetNameNextFrame;
    private bool _presetNameDialogOpen = true;
    private PresetNameMode _presetNameMode;
    private string? _renamePresetId;
    private string _presetNameError = string.Empty;
    private NativeCore.InventoryView? _pendingBodyItem;
    private NativeCore.InventoryView? _pendingTransferItem;
    private bool _requestBodyDialogOpen;
    private bool _requestTransferDialogOpen;
    private bool _bodyDialogOpen = true;
    private bool _transferDialogOpen = true;
    private bool _suppressTransferPrompt;

    private bool DrawVirtualSlots(uint characterHash, bool canEdit, bool english)
    {
        bool requestPickerPopup = false;
        ImGui.BeginDisabled(!canEdit);
        for (int index = 0; index < ActiveVirtualSlotCount; ++index)
        {
            ImGui.PushID_Int(index);
            ImGui.Text($"{NativeSlotCount + index:00}");
            ImGui.SameLine(0.0f, -1.0f);

            if (_presetConflicts.TryGetValue((characterHash, index), out var conflict))
            {
                ImGui.TextColored(_conflictColor, PresetConflictText(conflict, english));
                ImGui.SameLine(0.0f, -1.0f);
                if (ImGui.SmallButton(english ? "Replace" : "更换"))
                    requestPickerPopup = PreparePicker(index);
            }
            else
            {
                uint slotId = _selection[index];
                string label = GetSelectedLabel(slotId, english);
                if (ImGui.Button(label, _buttonSize))
                    requestPickerPopup = PreparePicker(index);
            }

            ImGui.SameLine(0.0f, -1.0f);
            if (ImGui.SmallButton("X"))
                ClearVirtualSlot(characterHash, index);
            ImGui.PopID();
        }
        ImGui.EndDisabled();
        return requestPickerPopup;
    }

    private bool PreparePicker(int slot)
    {
        _pickerSlot = slot;
        _pickerOpen = true;
        _usageFilter = InventoryUsageFilter.Unused;
        _pendingBodyItem = null;
        _pendingTransferItem = null;
        _requestBodyDialogOpen = false;
        _requestTransferDialogOpen = false;
        Array.Clear(_searchBuffer);
        RefreshInventory();
        return true;
    }

    private void DrawPresetBar(uint characterHash, bool canEdit, bool english)
    {
        SigilPreset? selected = ResolveSelectedPreset();
        string selectedName = selected?.Name ?? (english ? "<none>" : "<无>");
        ImGui.Text(english ? $"Current preset: {selectedName}" : $"当前预设：{selectedName}");
        ImGui.TextWrapped(
            english
                ? "Presets always retain all 24 slots. Reducing the active count does not delete higher preset slots, and they can be applied again after expanding."
                : "预设始终保留全部 24 个槽位。缩减生效数量不会删除高位预设，重新扩展后可再次套用。");

        ImGui.BeginDisabled(_presetStore.Presets.Count < 2);
        if (ImGui.SmallButton("<##preset_previous"))
            CyclePreset(-1);
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.SmallButton(">##preset_next"))
            CyclePreset(1);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginDisabled(!canEdit || selected is null);
        if (ImGui.SmallButton(english ? "Apply preset##preset_apply" : "套用预设##preset_apply"))
            ApplySelectedPreset(characterHash, english);
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.SmallButton(english ? "Overwrite##preset_overwrite" : "覆盖保存##preset_overwrite"))
            OverwriteSelectedPreset(english);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginDisabled(!canEdit);
        if (ImGui.SmallButton(english ? "Save as##preset_save_as" : "另存为##preset_save_as"))
            QueuePresetNameDialog(PresetNameMode.Create, null, string.Empty);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.SmallButton(english ? "Manage##preset_manage" : "管理预设##preset_manage"))
        {
            _presetManagerOpen = true;
            ImGui.OpenPopupStr(PresetManagerTitle(english), 0);
        }

        if (_presetStatus.Length != 0)
        {
            ImGui.TextColored(
                _presetStatusIsError ? _conflictColor : _successColor,
                _presetStatus);
        }
    }

    private void DrawPresetManager(uint characterHash, bool canEdit, bool english)
    {
        string title = PresetManagerTitle(english);
        ImGui.SetNextWindowSize(_presetManagerSize, 1 << 3);
        if (!ImGui.BeginPopupModal(
                title,
                ref _presetManagerOpen,
                ImGuiWindowFlagsNoSavedSettings))
            return;

        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        SigilPreset? selected = ResolveSelectedPreset();
        ImGui.BeginChildStr("PresetList##GBFRES", _presetManagerChildSize, true, 0);
        for (int index = 0; index < _presetStore.Presets.Count; ++index)
        {
            SigilPreset preset = _presetStore.Presets[index];
            string visibleName = preset.Name.Replace("##", "# #", StringComparison.Ordinal);
            string label = $"{visibleName}##preset_{preset.Id}";
            if (ImGui.SelectableBool(
                    label,
                    string.Equals(preset.Id, selected?.Id, StringComparison.Ordinal),
                    0,
                    _zeroSize))
            {
                _selectedPresetId = preset.Id;
                selected = preset;
            }
        }
        ImGui.EndChild();

        ImGui.BeginDisabled(!canEdit || selected is null);
        if (ImGui.Button(english ? "Apply" : "套用", _zeroSize))
        {
            ApplySelectedPreset(characterHash, english);
            ImGui.CloseCurrentPopup();
            _presetManagerOpen = false;
        }
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginDisabled(!canEdit);
        if (ImGui.Button(english ? "New" : "新建", _zeroSize))
        {
            QueuePresetNameDialog(PresetNameMode.Create, null, string.Empty);
            ImGui.CloseCurrentPopup();
            _presetManagerOpen = false;
        }
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginDisabled(selected is null);
        if (ImGui.Button(english ? "Rename" : "重命名", _zeroSize) && selected is not null)
        {
            QueuePresetNameDialog(PresetNameMode.Rename, selected.Id, selected.Name);
            ImGui.CloseCurrentPopup();
            _presetManagerOpen = false;
        }
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Delete" : "删除", _zeroSize) && selected is not null)
            DeleteSelectedPreset(selected, english);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        if (ImGui.Button(english ? "Close" : "关闭", _zeroSize))
        {
            ImGui.CloseCurrentPopup();
            _presetManagerOpen = false;
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private void QueuePresetNameDialog(
        PresetNameMode mode,
        string? renamePresetId,
        string initialName)
    {
        _presetNameMode = mode;
        _renamePresetId = renamePresetId;
        _presetNameError = string.Empty;
        SetUtf8BufferText(_presetNameBuffer, initialName);
        _presetNameDialogOpen = true;
        _openPresetNameNextFrame = true;
    }

    private void DrawPresetNameDialog(bool english)
    {
        if (_presetNameMode == PresetNameMode.None)
            return;

        string title = PresetNameTitle(english);
        if (_openPresetNameNextFrame)
        {
            _openPresetNameNextFrame = false;
            ImGui.OpenPopupStr(title, 0);
        }
        ImGui.SetNextWindowSize(_dialogSize, 1 << 3);
        if (!ImGui.BeginPopupModal(
                title,
                ref _presetNameDialogOpen,
                ImGuiWindowFlagsNoSavedSettings))
        {
            if (!_presetNameDialogOpen)
                _presetNameMode = PresetNameMode.None;
            return;
        }

        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        ImGui.Text(english ? "Preset name" : "预设名称");
        ImGui.SetNextItemWidth(-1.0f);
        fixed (byte* nameBuffer = _presetNameBuffer)
        {
            ImGui.InputTextWithHint(
                "##preset_name",
                english ? "Enter a custom preset name" : "输入自定义预设名称",
                (sbyte*)nameBuffer,
                (IntPtr)_presetNameBuffer.Length,
                0,
                null!,
                IntPtr.Zero);
        }
        if (!english && RepairChineseBuffer(_presetNameBuffer))
        {
            ImGui.ClearActiveID();
            ImGui.SetKeyboardFocusHere(-1);
        }
        if (_presetNameError.Length != 0)
            ImGui.TextColored(_conflictColor, _presetNameError);

        if (ImGui.Button(english ? "Save" : "保存", _zeroSize))
            SavePresetNameDialog(english);
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Cancel" : "取消", _zeroSize))
        {
            _presetNameMode = PresetNameMode.None;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private void SavePresetNameDialog(bool english)
    {
        string name = GetUtf8BufferText(_presetNameBuffer).Trim();
        if (name.Length == 0)
        {
            _presetNameError = english ? "Preset name cannot be empty." : "预设名称不能为空。";
            return;
        }
        if (name.Length > SigilPresetStore.MaximumNameLength)
        {
            _presetNameError = english
                ? $"Preset name cannot exceed {SigilPresetStore.MaximumNameLength} characters."
                : $"预设名称不能超过 {SigilPresetStore.MaximumNameLength} 个字符。";
            return;
        }
        if (_presetStore.NameExists(
                name,
                _presetNameMode == PresetNameMode.Rename ? _renamePresetId : null))
        {
            _presetNameError = english
                ? "A preset with that name already exists."
                : "已经存在同名预设。";
            return;
        }

        try
        {
            if (_presetNameMode == PresetNameMode.Create)
            {
                SigilPreset created = _presetStore.Create(name);
                _selectedPresetId = created.Id;
                SetPresetStatus(
                    english ? $"Saved preset: {created.Name}" : $"已保存预设：{created.Name}",
                    false);
            }
            else
            {
                SigilPreset? preset = _presetStore.FindById(_renamePresetId);
                if (preset is null)
                    throw new InvalidOperationException("The preset no longer exists.");
                _presetStore.Rename(preset, name);
                _selectedPresetId = preset.Id;
                SetPresetStatus(
                    english ? $"Renamed preset: {preset.Name}" : $"已重命名预设：{preset.Name}",
                    false);
            }
            _presetNameMode = PresetNameMode.None;
            ImGui.CloseCurrentPopup();
        }
        catch (Exception exception)
        {
            _log($"Preset name operation failed: {exception}");
            _presetNameError = english ? "Could not save the preset." : "保存预设失败。";
        }
    }

    private void ApplySelectedPreset(uint characterHash, bool english)
    {
        SigilPreset? preset = ResolveSelectedPreset();
        if (preset is null)
            return;

        try
        {
            IReadOnlyDictionary<uint, uint[]> selections =
                _presetStore.GetSelections(preset);
            int requested = selections.Values.Sum(slots =>
                slots.Count(slotId => slotId != 0));
            NativeCore.PresetApplySummary? summary = NativeCore.ApplyPreset(
                selections,
                ActiveVirtualSlotCount);
            if (summary is null)
            {
                SetPresetStatus(
                    english ? "Could not apply the preset in the current state."
                            : "当前状态无法套用预设。",
                    true);
                return;
            }

            _presetConflicts.Clear();
            int applied = 0;
            int conflicts = 0;
            foreach (NativeCore.PresetSlotResult result in summary.SlotResults)
            {
                if (result.RequestedSlotId == 0)
                    continue;
                if (result.Status == NativeCore.PresetSlotStatus.Applied)
                {
                    ++applied;
                    continue;
                }
                ++conflicts;
                _presetConflicts[(result.CharacterHash, result.VirtualSlot)] = result;
            }

            SetPresetStatus(
                english
                    ? $"Preset applied: {applied}/{requested} sigils, {conflicts} conflicts."
                    : $"预设已套用：{applied}/{requested} 个因子，{conflicts} 个冲突。",
                conflicts != 0);
            LoadSelection(characterHash);
            RefreshInventory();
        }
        catch (Exception exception)
        {
            _log($"Preset apply failed: {exception}");
            SetPresetStatus(english ? "Preset apply failed." : "套用预设失败。", true);
        }
    }

    private void OverwriteSelectedPreset(bool english)
    {
        SigilPreset? preset = ResolveSelectedPreset();
        if (preset is null)
            return;
        try
        {
            _presetStore.Overwrite(preset);
            SetPresetStatus(
                english ? $"Updated preset: {preset.Name}" : $"已更新预设：{preset.Name}",
                false);
        }
        catch (Exception exception)
        {
            _log($"Preset overwrite failed: {exception}");
            SetPresetStatus(english ? "Could not update the preset." : "更新预设失败。", true);
        }
    }

    private void DeleteSelectedPreset(SigilPreset preset, bool english)
    {
        try
        {
            string deletedName = preset.Name;
            _presetStore.Delete(preset);
            _selectedPresetId = _presetStore.Presets.FirstOrDefault()?.Id;
            SetPresetStatus(
                english ? $"Deleted preset: {deletedName}" : $"已删除预设：{deletedName}",
                false);
        }
        catch (Exception exception)
        {
            _log($"Preset delete failed: {exception}");
            SetPresetStatus(english ? "Could not delete the preset." : "删除预设失败。", true);
        }
    }

    private SigilPreset? ResolveSelectedPreset()
    {
        SigilPreset? selected = _presetStore.FindById(_selectedPresetId);
        if (selected is not null)
            return selected;
        selected = _presetStore.Presets.FirstOrDefault();
        _selectedPresetId = selected?.Id;
        return selected;
    }

    private void CyclePreset(int direction)
    {
        if (_presetStore.Presets.Count == 0)
            return;
        SigilPreset? selected = ResolveSelectedPreset();
        int index = selected is null
            ? 0
            : _presetStore.Presets
                .Select((preset, presetIndex) => (preset, presetIndex))
                .First(pair => pair.preset.Id == selected.Id)
                .presetIndex;
        index = (index + direction + _presetStore.Presets.Count) % _presetStore.Presets.Count;
        _selectedPresetId = _presetStore.Presets[index].Id;
    }
}
