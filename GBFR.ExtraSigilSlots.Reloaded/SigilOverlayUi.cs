using DearImguiSharp;
using Reloaded.Imgui.Hook;
using System.Runtime.InteropServices;
using System.Text;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal sealed unsafe partial class SigilOverlayUi : IDisposable
{
    private const int NativeSlotCount = 13;
    private const int ImGuiCondFirstUseEver = 1 << 2;
    private const int ImGuiWindowFlagsNoCollapse = 1 << 5;
    private const int ImGuiWindowFlagsNoSavedSettings = 1 << 8;
    private const int MaxInventoryCount = 5100;

    private readonly Action<bool> _setInputCapture;
    private readonly Action<string> _log;
    private readonly bool _brokerOwnsMouseCapture;
    private readonly MouseInteractionGate _mouseInteractionGate = new();
    private readonly List<NativeCore.InventoryView> _inventory = [];
    private readonly Dictionary<uint, NativeCore.InventoryView> _inventoryBySlot = [];
    private readonly List<int> _filteredIndices = [];
    private readonly byte[] _searchBuffer = new byte[160];
    private readonly byte[] _labelBuffer = new byte[1024];
    private readonly ImVec2 _zeroSize = MakeVec2(0.0f, 0.0f);
    private readonly ImVec2 _windowPosition = MakeVec2(0.0f, 0.0f);
    private readonly ImVec2 _windowSize = MakeVec2(780.0f, 650.0f);
    private readonly ImVec2 _buttonSize = MakeVec2(-45.0f, 0.0f);
    private readonly ImVec2 _pickerSize = MakeVec2(1000.0f, 700.0f);
    private readonly ImVec2 _childSize = MakeVec2(0.0f, -76.0f);
    private readonly ImVec4 _successColor = MakeVec4(0.35f, 1.0f, 0.45f, 1.0f);
    private readonly ImVec4 _readOnlyColor = MakeVec4(1.0f, 0.8f, 0.2f, 1.0f);

    private NativeCore.RuntimeState _state;
    private uint[] _selection = new uint[NativeCore.VirtualSlotCapacity];
    private uint _selectionCharacterHash;
    private bool _windowOpen;
    private int _pickerSlot = -1;
    private bool _pickerOpen = true;
    private bool _hasSavedClipRect;
    private NativeRect _savedClipRect;
    private IntPtr _savedCaptureWindow;
    private bool _disposed;

    internal SigilOverlayUi(
        string presetDirectory,
        Action<bool> setInputCapture,
        Action<string> log,
        bool brokerOwnsMouseCapture = false)
    {
        _presetStore = new SigilPresetStore(presetDirectory, log);
        _setInputCapture = setInputCapture;
        _log = log;
        _brokerOwnsMouseCapture = brokerOwnsMouseCapture;
    }

    internal void RenderFrame()
    {
        if (_disposed)
            return;

        bool openedThisFrame = false;
        if (FrontendOverlayGate.ConsumeToggleRequest())
        {
            SetWindowOpen(!_windowOpen);
            openedThisFrame = _windowOpen;
        }

        if (!_brokerOwnsMouseCapture)
            ImGui.GetIO().MouseDrawCursor = _windowOpen;
        if (!_windowOpen)
            return;

        _mouseInteractionGate.Observe(
            MouseButtonStateTracker.PressedButtons,
            MouseButtonStateTracker.ButtonEventSequence);

        if (!NativeCore.TryGetState(out _state))
        {
            SetWindowOpen(false);
            return;
        }
        FrontendOverlayGate.SetToggleKey(_state.ToggleKey);
        if (openedThisFrame)
        {
            RefreshInventory();
            InitializeVirtualSlotCountUi();
        }
        else if (_inventory.Count == 0 || NativeCore.IsInventoryDirty())
            RefreshInventory();

        uint characterHash = _state.EffectiveCharacterHash;
        bool canEdit = characterHash != 0 && _state.EditAllowed != 0;
        if (characterHash != _selectionCharacterHash)
            LoadSelection(characterHash);

        ImVec2 display = ImGui.GetIO().DisplaySize;
        _windowPosition.X = Math.Max(0.0f, display.X * 0.69f);
        _windowPosition.Y = Math.Max(0.0f, display.Y * 0.54f);
        ImGui.SetNextWindowPos(_windowPosition, ImGuiCondFirstUseEver, _zeroSize);
        ImGui.SetNextWindowSize(_windowSize, ImGuiCondFirstUseEver);

        bool english = UiLocalization.IsEnglish(_state.Language);
        bool open = _windowOpen;
        if (!ImGui.Begin(
                english
                    ? "GBFR Extra Sigil Slots##Reloaded"
                    : "GBFR 扩展因子槽##Reloaded",
                ref open,
                ImGuiWindowFlagsNoCollapse))
        {
            ImGui.End();
            SetWindowOpen(open);
            return;
        }
        SetWindowOpen(open);
        if (!open)
        {
            ImGui.End();
            return;
        }

        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        english = DrawLanguageSelector(english);
        string characterName = UiLocalization.CharacterName(characterHash, english);
        ImGui.Text(
            english
                ? $"Current character: {characterName}"
                : $"当前角色：{characterName}"
        );

        if (ImGui.SmallButton(english ? "Refresh sigils" : "刷新因子"))
            RefreshInventory();
        ImGui.SameLine(0.0f, -1.0f);
        ImGui.Text(
            english
                ? $"Scanned sigils: {_inventory.Count}"
                : $"扫描出因子数量：{_inventory.Count}"
        );
        ImGui.TextColored(
            canEdit ? _successColor : _readOnlyColor,
            english
                ? canEdit ? "Current state: editable" : "Current state: read-only"
                : canEdit ? "当前状态可修改" : "当前状态不可修改"
        );
        ImGui.TextColored(
            _readOnlyColor,
            english
                ? "The game does not support hot-updating sigils during battle. Thank you for understanding."
                : "游戏不支持战斗状态热更新因子，请谅解"
        );
        DrawVirtualSlotCountSetting(english);

        ImGui.Separator();
        DrawPresetBar(characterHash, canEdit, english);

        ImGui.Separator();
        bool requestPickerPopup = DrawVirtualSlots(characterHash, canEdit, english);
        ImGui.EndDisabled();
        string pickerTitle = english
            ? "Select an inventory sigil##Reloaded"
            : "选择库存因子##Reloaded";
        if (requestPickerPopup)
            ImGui.OpenPopupStr(pickerTitle, 0);

        DrawPicker(characterHash, canEdit, english, pickerTitle);
        DrawPresetManager(characterHash, canEdit, english);
        DrawPresetNameDialog(english);
        DrawVirtualSlotCountConfirmation(english);
        ImGui.End();
    }

    internal void Close()
    {
        FrontendOverlayGate.ForceClosed();
        SetWindowOpen(false);
        _pickerSlot = -1;
    }

    private void DrawPicker(
        uint characterHash,
        bool canEdit,
        bool english,
        string pickerTitle)
    {
        if (_pickerSlot < 0 || _pickerSlot >= ActiveVirtualSlotCount)
            return;

        if (!canEdit)
            _pickerOpen = false;

        ImGui.SetNextWindowSize(_pickerSize, 1 << 3);
        if (!ImGui.BeginPopupModal(
                pickerTitle,
                ref _pickerOpen,
                ImGuiWindowFlagsNoSavedSettings))
        {
            if (!_pickerOpen)
                _pickerSlot = -1;
            return;
        }

        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        ImGui.Text(
            english
                ? $"Extra slot {NativeSlotCount + _pickerSlot}"
                : $"额外槽 {NativeSlotCount + _pickerSlot}"
        );
        ImGui.SetNextItemWidth(-190.0f);
        fixed (byte* searchBuffer = _searchBuffer)
        {
            ImGui.InputTextWithHint(
                "##sigil_search",
                english ? "Search sigil name or trait" : "搜索因子名称或特性",
                (sbyte*)searchBuffer,
                (IntPtr)_searchBuffer.Length,
                0,
                null!,
                IntPtr.Zero
            );
        }
        if (!english && RepairChineseBuffer(_searchBuffer))
        {
            // InputText keeps an internal edit buffer while active. Releasing and
            // refocusing it makes the corrected UTF-8 user buffer authoritative.
            ImGui.ClearActiveID();
            ImGui.SetKeyboardFocusHere(-1);
        }
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Refresh sigils" : "刷新因子", _zeroSize))
            RefreshInventory();

        DrawUsageFilterButtons(english);
        BuildFilter(characterHash);
        ImGui.Separator();
        ImGui.Text(
            english
                ? $"Matching sigils: {_filteredIndices.Count}"
                : $"匹配的因子：{_filteredIndices.Count}"
        );
        ImGui.BeginChildStr("SigilInventory##Reloaded", _childSize, true, 0);
        using (var clipper = new ImGuiListClipper())
        {
            ImGui.ImGuiListClipperBegin(clipper, _filteredIndices.Count, -1.0f);
            while (ImGui.ImGuiListClipperStep(clipper))
            {
                for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; ++row)
                {
                    NativeCore.InventoryView item = _inventory[_filteredIndices[row]];
                    string label =
                        $"{BuildInventoryDisplayLabel(item, characterHash, english)}" +
                        $"##inventory_{item.Gem.SlotId}";
                    if (ImGui.SelectableBool(label, false, 0, _zeroSize))
                        HandleInventoryItemClick(item, characterHash, english);
                }
            }
            ImGui.ImGuiListClipperEnd(clipper);
        }
        ImGui.EndChild();
        ImGui.EndDisabled();

        OpenRequestedInventoryConflictDialogs(english);
        if (DrawInventoryConflictDialogs(characterHash, english))
            ClosePickerPopup();

        ImGui.BeginDisabled(!_mouseInteractionGate.IsArmed);
        if (ImGui.Button(english ? "Clear this slot" : "清空此槽", _zeroSize))
        {
            ClearVirtualSlot(characterHash, _pickerSlot);
            ClosePickerPopup();
        }
        ImGui.SameLine(0.0f, -1.0f);
        if (ImGui.Button(english ? "Cancel" : "取消", _zeroSize))
            ClosePickerPopup();
        ImGui.EndDisabled();
        ImGui.EndPopup();
        if (!_pickerOpen)
            _pickerSlot = -1;
    }

    private bool DrawLanguageSelector(bool english)
    {
        ImGui.Text(english ? "Current language: English" : "当前语言：中文");
        ImGui.SameLine(0.0f, -1.0f);
        ImGui.BeginDisabled(!english);
        if (ImGui.SmallButton("中文##language_zh"))
            english = ChangeLanguage(UiLocalization.Chinese);
        ImGui.EndDisabled();
        ImGui.SameLine(0.0f, -1.0f);
        ImGui.BeginDisabled(english);
        if (ImGui.SmallButton("English##language_en"))
            english = ChangeLanguage(UiLocalization.English);
        ImGui.EndDisabled();
        return english;
    }

    private bool ChangeLanguage(int language)
    {
        if (NativeCore.SetLanguage(language))
        {
            _state.Language = language;
            RefreshInventory();
        }
        return UiLocalization.IsEnglish(_state.Language);
    }

    private uint ResolveDisplayCharacterHash()
    {
        return _state.UiSelectedCharacterHash != 0
            ? _state.UiSelectedCharacterHash
            : _state.EffectiveCharacterHash;
    }

    private void RefreshInventory()
    {
        if (!NativeCore.RefreshInventory())
        {
            _inventory.Clear();
            _inventoryBySlot.Clear();
            return;
        }

        uint count = Math.Min(NativeCore.GetInventoryCount(), MaxInventoryCount);
        _inventory.Clear();
        _inventoryBySlot.Clear();
        for (uint index = 0; index < count; ++index)
        {
            Array.Clear(_labelBuffer);
            if (!NativeCore.TryCopyInventoryItem(index, _labelBuffer, out var item) || item is null)
                continue;
            _inventory.Add(item);
            _inventoryBySlot[item.Gem.SlotId] = item;
        }
    }

    private void LoadSelection(uint characterHash)
    {
        _selectionCharacterHash = characterHash;
        _selection = characterHash == 0
            ? new uint[NativeCore.VirtualSlotCapacity]
            : NativeCore.GetSelection(characterHash);
    }

    private int ActiveVirtualSlotCount => _state.VirtualSlotCount is >= 1 and <= NativeCore.VirtualSlotCapacity
        ? (int)_state.VirtualSlotCount
        : NativeCore.DefaultVirtualSlotCount;

    private string GetSelectedLabel(uint slotId, bool english)
    {
        if (slotId == 0)
            return english ? "<empty>" : "<空>";
        return _inventoryBySlot.TryGetValue(slotId, out var item)
            ? item.Label
            : english
                ? $"<missing inventory slot #{slotId}>"
                : $"<缺失库存槽 #{slotId}>";
    }

    private void BuildFilter(uint characterHash)
    {
        string search = GetSearchText().ToLowerInvariant();
        _filteredIndices.Clear();
        for (int index = 0; index < _inventory.Count; ++index)
        {
            NativeCore.InventoryView item = _inventory[index];
            if (item.RequiredCharacterHash != 0 &&
                item.RequiredCharacterHash != characterHash)
                continue;
            bool bodyUsed = item.Equipped;
            bool extensionUsed = item.VirtualOwnerCharacterHash != 0;
            bool included = _usageFilter switch
            {
                InventoryUsageFilter.All => true,
                InventoryUsageFilter.Used => bodyUsed || extensionUsed,
                InventoryUsageFilter.BodyUsed => bodyUsed,
                InventoryUsageFilter.ExtensionUsed => extensionUsed,
                _ => !bodyUsed && !extensionUsed,
            };
            if (!included)
                continue;
            if (search.Length != 0 && !item.Searchable.Contains(search, StringComparison.Ordinal))
                continue;
            _filteredIndices.Add(index);
        }
    }

    private string GetSearchText()
    {
        int length = Array.IndexOf(_searchBuffer, (byte)0);
        if (length < 0)
            length = _searchBuffer.Length;
        return Encoding.UTF8.GetString(_searchBuffer, 0, length);
    }

    private static bool RepairChineseBuffer(byte[] buffer)
    {
        int length = Array.IndexOf(buffer, (byte)0);
        if (length < 0)
            length = buffer.Length;
        string current = Encoding.UTF8.GetString(buffer, 0, length);
        string repaired = RepairChineseMojibake(current);
        if (string.Equals(current, repaired, StringComparison.Ordinal))
            return false;

        byte[] utf8 = Encoding.UTF8.GetBytes(repaired);
        if (utf8.Length >= buffer.Length)
            return false;
        Array.Clear(buffer);
        utf8.CopyTo(buffer, 0);
        return true;
    }

    internal static string RepairChineseMojibake(string current)
    {
        if (current.Length < 2)
            return current;

        StringBuilder repaired = new(current.Length);
        bool changed = false;
        for (int index = 0; index < current.Length; ++index)
        {
            char first = current[index];
            if (index + 1 < current.Length &&
                first is >= '\u0081' and <= '\u00FE' &&
                current[index + 1] <= '\u00FE')
            {
                char second = current[index + 1];
                byte trail = (byte)second;
                bool validGbkTrail = trail is >= 0x40 and <= 0xFE && trail != 0x7F;
                ushort packed = (ushort)((first << 8) | second);
                if (validGbkTrail &&
                    Mod.TryDecodeAnsiCharacter(936, packed, out ushort decoded) &&
                    IsChineseSearchCharacter(decoded))
                {
                    repaired.Append((char)decoded);
                    ++index;
                    changed = true;
                    continue;
                }
            }
            repaired.Append(first);
        }

        return changed ? repaired.ToString() : current;
    }

    private static bool IsChineseSearchCharacter(ushort character)
    {
        return character is >= 0x3400 and <= 0x9FFF or
            >= 0xF900 and <= 0xFAFF or
            >= 0x3000 and <= 0x303F or
            >= 0xFF00 and <= 0xFFEF;
    }

    private void SetWindowOpen(bool open)
    {
        FrontendOverlayGate.SetOpen(open);
        bool changed = _windowOpen != open;
        if (!changed)
            return;

        _windowOpen = open;
        _setInputCapture(open);
        if (open)
        {
            _mouseInteractionGate.Open(MouseButtonStateTracker.ButtonEventSequence);
            if (!_brokerOwnsMouseCapture)
            {
                ResetImGuiMouseState();
                BeginReleasedMouse();
            }
        }
        else
        {
            _mouseInteractionGate.Close();
            if (!_brokerOwnsMouseCapture)
                RestoreMouseCapture();
        }
        if (!open)
        {
            _pickerSlot = -1;
            _pendingBodyItem = null;
            _pendingTransferItem = null;
            _requestBodyDialogOpen = false;
            _requestTransferDialogOpen = false;
            _suppressTransferPrompt = false;
            _slotCountConfirmationOpen = false;
            _openSlotCountConfirmationNextFrame = false;
        }
    }

    private void BeginReleasedMouse()
    {
        _hasSavedClipRect = GetClipCursor(out _savedClipRect);
        _savedCaptureWindow = GetCapture();
        if (_savedCaptureWindow != IntPtr.Zero)
            ReleaseCapture();
        ClipCursor(IntPtr.Zero);
    }

    private static void ResetImGuiMouseState()
    {
        var io = ImGui.GetIO();
        ImGui.ImGuiIO_ClearInputKeys(io);
        for (int button = 0; button < 5; ++button)
            ImGui.ImGuiIO_AddMouseButtonEvent(io, button, false);
        ImGui.ClearActiveID();
    }

    private void RestoreMouseCapture()
    {
        ReleaseCapture();
        if (_hasSavedClipRect)
            ClipCursorRect(ref _savedClipRect);
        if (_savedCaptureWindow != IntPtr.Zero && IsWindow(_savedCaptureWindow))
            SetCapture(_savedCaptureWindow);
        IntPtr gameWindow = ImguiHook.WindowHandle;
        if (gameWindow != IntPtr.Zero && IsWindow(gameWindow))
            SetForegroundWindow(gameWindow);
        _hasSavedClipRect = false;
        _savedCaptureWindow = IntPtr.Zero;
    }

    private static ImVec2 MakeVec2(float x, float y)
    {
        ImVec2 value = new();
        value.X = x;
        value.Y = y;
        return value;
    }

    private static ImVec4 MakeVec4(float x, float y, float z, float w)
    {
        ImVec4 value = new();
        value.X = x;
        value.Y = y;
        value.Z = z;
        value.W = w;
        return value;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Close();
        _zeroSize.Dispose();
        _windowPosition.Dispose();
        _windowSize.Dispose();
        _buttonSize.Dispose();
        _pickerSize.Dispose();
        _childSize.Dispose();
        _successColor.Dispose();
        _readOnlyColor.Dispose();
        DisposePresetUiResources();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClipCursor(out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClipCursor(IntPtr rectangle);

    [DllImport("user32.dll", EntryPoint = "ClipCursor")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClipCursorRect(ref NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr GetCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SetCapture(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
}
