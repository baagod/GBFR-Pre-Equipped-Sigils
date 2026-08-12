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
    private readonly ImVec2 _presetManagerSize = MakeVec2(800.0f, 540.0f);
    private readonly ImVec2 _presetManagerCharacterListSize = MakeVec2(250.0f, -48.0f);
    private readonly ImVec2 _presetManagerPresetListSize = MakeVec2(0.0f, -48.0f);
    private readonly ImVec2 _presetTransferDialogSize = MakeVec2(500.0f, 520.0f);
    private readonly ImVec2 _presetTransferCharacterListSize = MakeVec2(0.0f, 330.0f);
    private readonly ImVec2 _dialogSize = MakeVec2(650.0f, 0.0f);
    private readonly ImVec4 _conflictColor = MakeVec4(1.0f, 0.35f, 0.3f, 1.0f);

    private readonly Dictionary<(uint CharacterHash, int Slot),
        NativeCore.PresetSlotResult> _presetConflicts = [];
    private InventoryUsageFilter _usageFilter = InventoryUsageFilter.Unused;
    private string _presetStatus = string.Empty;
    private bool _presetStatusIsError;
    private bool _presetManagerOpen = true;
    private uint _presetManagerCharacterHash;
    private string? _presetManagerSelectedPresetId;
    private bool _openPresetNameNextFrame;
    private bool _presetNameDialogOpen = true;
    private PresetNameMode _presetNameMode;
    private uint _presetNameCharacterHash;
    private string? _renamePresetId;
    private string _presetNameError = string.Empty;
    private bool _openPresetTransferNextFrame;
    private bool _presetTransferDialogOpen = true;
    private string? _presetTransferPresetId;
    private string _presetTransferError = string.Empty;
    private uint _presetTransferTargetHash;
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
        ResetMouseInteractionBoundary();
        return true;
    }

    private void DrawPresetBar(uint characterHash, bool canEdit, bool english)
    {
        SigilPreset? selected = ResolveSelectedPreset(characterHash);
        string selectedName = selected?.Name ?? (english ? "Temporary preset" : "临时预设");
        IReadOnlyList<SigilPreset> characterPresets =
            _presetStore.GetPresetsForCharacter(characterHash);
        ImGui.Text(english ? $"Current preset: {selectedName}" : $"当前预设：{selectedName}");
        ImGui.TextWrapped(
            english
                ? "Presets always retain all 24 slots. Reducing the active count does not delete higher preset slots, and they can be applied again after expanding."
                : "预设始终保留全部 24 个槽位。缩减生效数量不会删除高位预设，重新扩展后可再次套用。");

        ImGui.BeginDisabled(characterPresets.Count == 0);
        if (ImGui.SmallButton("<##preset_previous"))
            CyclePreset(characterHash, -1, english);
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.SmallButton(">##preset_next"))
            CyclePreset(characterHash, 1, english);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginDisabled(!canEdit || selected is null);
        if (ImGui.SmallButton(english ? "Apply preset##preset_apply" : "套用预设##preset_apply"))
            ApplyPreset(selected!, characterHash, english);
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.SmallButton(english ? "Overwrite##preset_overwrite" : "覆盖保存##preset_overwrite"))
            OverwriteSelectedPreset(characterHash, english);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginDisabled(!canEdit);
        if (ImGui.SmallButton(english ? "Save as##preset_save_as" : "另存为##preset_save_as"))
            QueuePresetNameDialog(PresetNameMode.Create, characterHash, null, string.Empty);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.SmallButton(english ? "Manage##preset_manage" : "管理预设##preset_manage"))
            OpenPresetManager(characterHash, english);

        if (_presetStatus.Length != 0)
        {
            ImGui.TextColored(
                _presetStatusIsError ? _conflictColor : _successColor,
                _presetStatus);
        }
    }

    private void OpenPresetManager(uint characterHash, bool english)
    {
        uint[] knownCharacters = UiLocalization.KnownCharacterHashes;
        _presetManagerCharacterHash = knownCharacters.Contains(characterHash)
            ? characterHash
            : knownCharacters[0];
        SigilPreset? active = ResolveSelectedPreset(_presetManagerCharacterHash);
        _presetManagerSelectedPresetId = active?.Id ??
            _presetStore.GetPresetsForCharacter(_presetManagerCharacterHash).FirstOrDefault()?.Id;
        _presetManagerOpen = true;
        ImGui.OpenPopupStr(PresetManagerTitle(english), 0);
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
        if (_presetManagerCharacterHash == 0)
            _presetManagerCharacterHash = UiLocalization.KnownCharacterHashes[0];

        ImGui.Text(english ? "Characters" : "角色");
        ImGui.SameLine(265.0f, -1.0f);
        ImGui.Text(english ? "Presets" : "预设");

        ImGui.BeginChildStr(
            "PresetCharacters##GBFRES",
            _presetManagerCharacterListSize,
            true,
            0);
        foreach (uint listedCharacterHash in UiLocalization.KnownCharacterHashes)
        {
            int presetCount = _presetStore.GetPresetCount(listedCharacterHash);
            string label =
                $"{UiLocalization.CharacterName(listedCharacterHash, english)} ({presetCount})" +
                $"##preset_character_{listedCharacterHash:X8}";
            if (ImGui.SelectableBool(
                    label,
                    listedCharacterHash == _presetManagerCharacterHash,
                    0,
                    _zeroSize))
            {
                _presetManagerCharacterHash = listedCharacterHash;
                _presetManagerSelectedPresetId =
                    _presetStore.GetPresetsForCharacter(listedCharacterHash).FirstOrDefault()?.Id;
            }
        }
        ImGui.EndChild();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginChildStr(
            "PresetList##GBFRES",
            _presetManagerPresetListSize,
            true,
            0);
        IReadOnlyList<SigilPreset> managerPresets =
            _presetStore.GetPresetsForCharacter(_presetManagerCharacterHash);
        SigilPreset? selected = ResolveManagerPreset();
        foreach (SigilPreset preset in managerPresets)
        {
            string visibleName = preset.Name.Replace("##", "# #", StringComparison.Ordinal);
            string label = $"{visibleName}##preset_{preset.Id}";
            if (ImGui.SelectableBool(
                    label,
                    string.Equals(preset.Id, selected?.Id, StringComparison.Ordinal),
                    0,
                    _zeroSize))
            {
                _presetManagerSelectedPresetId = preset.Id;
                selected = preset;
            }
        }
        if (managerPresets.Count == 0)
            ImGui.TextDisabled(english ? "No presets" : "没有预设");
        ImGui.EndChild();

        bool managerIsCurrentCharacter = _presetManagerCharacterHash == characterHash;
        ImGui.BeginDisabled(!canEdit || !managerIsCurrentCharacter || selected is null);
        if (ImGui.Button(english ? "Apply" : "套用", _zeroSize) && selected is not null)
        {
            if (ApplyPreset(selected, characterHash, english))
            {
                ImGui.CloseCurrentPopup();
                _presetManagerOpen = false;
            }
        }
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginDisabled(!canEdit || !managerIsCurrentCharacter);
        if (ImGui.Button(english ? "New" : "新建", _zeroSize))
        {
            QueuePresetNameDialog(
                PresetNameMode.Create,
                _presetManagerCharacterHash,
                null,
                string.Empty);
            ImGui.CloseCurrentPopup();
            _presetManagerOpen = false;
        }
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);

        ImGui.BeginDisabled(selected is null);
        if (ImGui.Button(english ? "Rename" : "重命名", _zeroSize) && selected is not null)
        {
            QueuePresetNameDialog(
                PresetNameMode.Rename,
                selected.CharacterHash,
                selected.Id,
                selected.Name);
            ImGui.CloseCurrentPopup();
            _presetManagerOpen = false;
        }
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Delete" : "删除", _zeroSize) && selected is not null)
            DeleteSelectedPreset(selected, english);
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Transfer" : "转让", _zeroSize) && selected is not null)
        {
            QueuePresetTransferDialog(selected);
            ImGui.CloseCurrentPopup();
            _presetManagerOpen = false;
        }
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

    private SigilPreset? ResolveManagerPreset()
    {
        SigilPreset? selected = _presetStore.FindById(_presetManagerSelectedPresetId);
        if (selected is not null && selected.CharacterHash == _presetManagerCharacterHash)
            return selected;
        _presetManagerSelectedPresetId = null;
        return null;
    }

    private void QueuePresetTransferDialog(SigilPreset preset)
    {
        _presetTransferPresetId = preset.Id;
        _presetTransferTargetHash = FirstOtherCharacter(preset.CharacterHash);
        _presetTransferError = string.Empty;
        _presetTransferDialogOpen = true;
        _openPresetTransferNextFrame = true;
    }

    private void DrawPresetTransferDialog(bool english)
    {
        string title = PresetTransferTitle(english);
        if (_openPresetTransferNextFrame)
        {
            _openPresetTransferNextFrame = false;
            ImGui.OpenPopupStr(title, 0);
        }

        ImGui.SetNextWindowSize(_presetTransferDialogSize, 1 << 3);
        if (!ImGui.BeginPopupModal(
                title,
                ref _presetTransferDialogOpen,
                ImGuiWindowFlagsNoSavedSettings))
            return;

        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        SigilPreset? preset = _presetStore.FindById(_presetTransferPresetId);
        if (preset is null)
        {
            ImGui.TextColored(
                _conflictColor,
                english ? "The preset no longer exists." : "当前预设已不存在。");
        }
        else
        {
            string sourceName = UiLocalization.CharacterName(preset.CharacterHash, english);
            ImGui.Text(english
                ? $"Preset: {preset.Name}"
                : $"预设：{preset.Name}");
            ImGui.Text(english
                ? $"From: {sourceName}"
                : $"来源角色：{sourceName}");
            ImGui.Text(english ? "Transfer to" : "转让给");

            ImGui.BeginChildStr(
                "PresetTransferTarget##GBFRES",
                _presetTransferCharacterListSize,
                true,
                0);
            foreach (uint targetCharacterHash in UiLocalization.KnownCharacterHashes)
            {
                ImGui.BeginDisabled(targetCharacterHash == preset.CharacterHash);
                int targetPresetCount = _presetStore.GetPresetCount(targetCharacterHash);
                string label =
                    $"{UiLocalization.CharacterName(targetCharacterHash, english)} ({targetPresetCount})" +
                    $"##preset_transfer_target_{targetCharacterHash:X8}";
                if (ImGui.SelectableBool(
                        label,
                        targetCharacterHash == _presetTransferTargetHash,
                        0,
                        _zeroSize))
                {
                    _presetTransferError = string.Empty;
                    _presetTransferTargetHash = targetCharacterHash;
                }
                ImGui.EndDisabled();
            }
            ImGui.EndChild();

            bool transferringActivePreset = IsSelectedPreset(preset);
            ImGui.TextWrapped(transferringActivePreset
                ? english
                    ? "The source character keeps its current slot contents as a temporary preset."
                    : "来源角色会保留当前槽位内容，并切换为临时预设。"
                : english
                    ? "The source character's current runtime preset is unchanged."
                    : "来源角色当前运行中的预设不会改变。");
        }
        if (_presetTransferError.Length != 0)
            ImGui.TextColored(_conflictColor, _presetTransferError);

        bool canTransfer = preset is not null &&
            _presetTransferTargetHash != 0 &&
            _presetTransferTargetHash != preset.CharacterHash;
        ImGui.BeginDisabled(!canTransfer);
        if (ImGui.Button(english ? "Confirm transfer" : "确认转让", _zeroSize))
            TransferSelectedPreset(english);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Cancel" : "取消", _zeroSize))
        {
            ImGui.CloseCurrentPopup();
            _presetTransferDialogOpen = false;
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private void TransferSelectedPreset(bool english)
    {
        SigilPreset? preset = _presetStore.FindById(_presetTransferPresetId);
        if (preset is null)
        {
            _presetTransferError = english
                ? "The preset no longer exists."
                : "当前预设已不存在。";
            return;
        }
        if (_presetStore.NameExists(_presetTransferTargetHash, preset.Name, preset.Id))
        {
            _presetTransferError = english
                ? "The target character already has a preset with that name."
                : "目标角色已经存在同名预设。";
            return;
        }

        try
        {
            string presetName = preset.Name;
            uint sourceCharacterHash = preset.CharacterHash;
            string sourceName = UiLocalization.CharacterName(sourceCharacterHash, english);
            string targetName = UiLocalization.CharacterName(_presetTransferTargetHash, english);
            ResolveSelectedPresetCore(sourceCharacterHash);
            _presetStore.TransferPreset(preset, _presetTransferTargetHash);

            _presetManagerCharacterHash = sourceCharacterHash;
            _presetManagerSelectedPresetId =
                _presetStore.GetPresetsForCharacter(sourceCharacterHash).FirstOrDefault()?.Id;
            SetPresetStatus(
                english
                    ? $"Transferred preset {presetName}: {sourceName} -> {targetName}."
                    : $"已转让预设 {presetName}：{sourceName} -> {targetName}。",
                false);
            _presetTransferPresetId = null;
            _presetTransferError = string.Empty;
            ImGui.CloseCurrentPopup();
            _presetTransferDialogOpen = false;
        }
        catch (Exception exception)
        {
            _log($"Preset transfer failed: {exception}");
            _presetTransferError = english ? "Preset transfer failed." : "预设转让失败。";
        }
    }

    private static uint FirstOtherCharacter(uint characterHash)
    {
        return UiLocalization.KnownCharacterHashes.First(hash => hash != characterHash);
    }

    private static string PresetTransferTitle(bool english) => english
        ? "Transfer preset##GBFRESPresetTransfer"
        : "转让预设##GBFRESPresetTransfer";

    private void QueuePresetNameDialog(
        PresetNameMode mode,
        uint characterHash,
        string? renamePresetId,
        string initialName)
    {
        _presetNameMode = mode;
        _presetNameCharacterHash = characterHash;
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
                _presetNameCharacterHash,
                name,
                _presetNameMode == PresetNameMode.Rename ? _renamePresetId : null))
        {
            _presetNameError = english
                ? "This character already has a preset with that name."
                : "当前角色已经存在同名预设。";
            return;
        }

        try
        {
            if (_presetNameMode == PresetNameMode.Create)
            {
                SigilPreset created = _presetStore.Create(_presetNameCharacterHash, name);
                _presetManagerCharacterHash = created.CharacterHash;
                _presetManagerSelectedPresetId = created.Id;
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
                _presetManagerCharacterHash = preset.CharacterHash;
                _presetManagerSelectedPresetId = preset.Id;
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

    private bool ApplyPreset(SigilPreset preset, uint characterHash, bool english)
    {
        if (preset.CharacterHash != characterHash)
            return false;

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
                return false;
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

            _presetStore.SelectPreset(preset);
            SetPresetStatus(
                english
                    ? $"Preset applied: {applied}/{requested} sigils, {conflicts} conflicts."
                    : $"预设已套用：{applied}/{requested} 个因子，{conflicts} 个冲突。",
                conflicts != 0);
            LoadSelection(characterHash);
            RefreshInventory();
            return true;
        }
        catch (Exception exception)
        {
            _log($"Preset apply failed: {exception}");
            SetPresetStatus(english ? "Preset apply failed." : "套用预设失败。", true);
            return false;
        }
    }

    private void OverwriteSelectedPreset(uint characterHash, bool english)
    {
        SigilPreset? preset = ResolveSelectedPreset(characterHash);
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
            uint characterHash = preset.CharacterHash;
            ResolveSelectedPresetCore(characterHash);
            _presetStore.Delete(preset);
            _presetManagerSelectedPresetId =
                _presetStore.GetPresetsForCharacter(characterHash).FirstOrDefault()?.Id;
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

    private SigilPreset? ResolveSelectedPreset(uint characterHash)
    {
        try
        {
            return ResolveSelectedPresetCore(characterHash);
        }
        catch (Exception exception)
        {
            _log($"Selected preset resolution failed: {exception}");
            return null;
        }
    }

    private SigilPreset? ResolveSelectedPresetCore(uint characterHash)
    {
        if (characterHash == 0)
            return null;

        IReadOnlyList<uint> currentSlots = characterHash == _selectionCharacterHash
            ? _selection
            : NativeCore.GetSelection(characterHash);
        return _presetStore.ResolveSelectedPreset(characterHash, currentSlots);
    }

    private bool IsSelectedPreset(SigilPreset preset)
    {
        try
        {
            ResolveSelectedPresetCore(preset.CharacterHash);
            return _presetStore.IsSelectedPreset(preset);
        }
        catch (Exception exception)
        {
            _log($"Selected preset check failed: {exception}");
            return false;
        }
    }

    private void CyclePreset(uint characterHash, int direction, bool english)
    {
        IReadOnlyList<SigilPreset> presets = _presetStore.GetPresetsForCharacter(characterHash);
        if (presets.Count == 0)
            return;
        SigilPreset? selected = ResolveSelectedPreset(characterHash);
        int index;
        if (selected is null)
        {
            index = direction < 0 ? presets.Count - 1 : 0;
        }
        else
        {
            index = presets
                .Select((preset, presetIndex) => (preset, presetIndex))
                .First(pair => pair.preset.Id == selected.Id)
                .presetIndex;
            index = (index + direction + presets.Count) % presets.Count;
        }

        try
        {
            _presetStore.SelectPreset(presets[index]);
        }
        catch (Exception exception)
        {
            _log($"Preset selection failed: {exception}");
            SetPresetStatus(
                english ? "Could not select the preset." : "无法选择预设。",
                true);
        }
    }
}
