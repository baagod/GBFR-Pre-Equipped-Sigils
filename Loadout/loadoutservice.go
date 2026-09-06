package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
)

// MaxSlots mirrors the managed editor limit (must stay in sync).
const MaxSlots = 12

// LoadoutService reads/writes the mod directory data files next to the exe.
// Protocol is shared with the mod: sigils.json (sigil table), skills.json
// (trait/skill dictionary), loadout.json (player config).
// (trait dictionary) and loadout.json (player configuration, new array
// format: [ { items: [{hash,level,zh,en}, {hash,level,zh,en}?], enabled } ]).
type LoadoutService struct{}

// MinimiseApp hides the window to the tray; the process stays alive so the
// in-game hotkey can bring the window back instantly. Invoked by the shared
// hotkey inside the tool and the X button (via the WM_CLOSE interceptor).
func (s *LoadoutService) MinimiseApp() {
	if win != nil {
		win.Hide()
	}
}

// GetHotkey returns the configured menu hotkey as a virtual key code.
// The mod publishes it in tool-hotkey.txt (mod directory, next to the exe);
// missing file falls back to F1 (0x70).
func (s *LoadoutService) GetHotkey() (int, error) {
	data, err := os.ReadFile(filepath.Join(exeDir(), "tool-hotkey.txt"))
	if err != nil {
		return 0x70, nil
	}
	if vk, err := strconv.Atoi(strings.TrimSpace(string(data))); err == nil && vk > 0 {
		return vk, nil
	}
	return 0x70, nil
}

type loadoutItem struct {
	Gem   string `json:"gem"` // items[0]: gem (物品) hash
	Hash  string `json:"hash"` // items[1]: trait (词条) hash
	Level int    `json:"level"`
	Zh    string `json:"zh"`
	En    string `json:"en"`
}

type loadoutSlot struct {
	Items   []loadoutItem `json:"items"`
	Enabled bool          `json:"enabled"`
}

func exeDir() string {
	exe, err := os.Executable()
	if err != nil {
		return "."
	}
	return filepath.Dir(exe)
}

// userCfgDir is where the player configuration (loadout.json) lives. The mod
// folder is replaced on every update; this location survives them.
func userCfgDir() string {
	base := os.Getenv("LOCALAPPDATA")
	if base == "" {
		if d, err := os.UserConfigDir(); err == nil {
			base = d
		} else {
			base = exeDir()
		}
	}
	return filepath.Join(base, "GBFRPreEquippedSigils")
}

func (s *LoadoutService) LoadTraits() (string, error) {
	data, err := os.ReadFile(filepath.Join(exeDir(), "skills.json"))
	if err != nil {
		return "", err
	}
	return string(data), nil
}

// LoadSigils returns the sigil table (sigils.json) used for the primary picker.
func (s *LoadoutService) LoadSigils() (string, error) {
	data, err := os.ReadFile(filepath.Join(exeDir(), "sigils.json"))
	if err != nil {
		return "", err
	}
	return string(data), nil
}

// LoadConfig returns the player configuration from the user directory; when
// none exists yet, the mod-dir pre-loadout.json template is used as the
// editable starting point.
func (s *LoadoutService) LoadConfig() (string, error) {
	data, err := os.ReadFile(filepath.Join(userCfgDir(), "loadout.json"))
	if err != nil {
		data, err = os.ReadFile(filepath.Join(exeDir(), "pre-loadout.json"))
		if err != nil {
			return "", err
		}
	}
	return string(data), nil
}

// ResetLoadout removes the player configuration so the mod falls back to the
// built-in template (matches LoadoutConfig's "file removed -> restore built-in"
// path). The UI reloads the preset itself. Deleting a non-existent file is a
// no-op.
func (s *LoadoutService) ResetLoadout() error {
	path := filepath.Join(userCfgDir(), "loadout.json")
	err := os.Remove(path)
	if err != nil && !os.IsNotExist(err) {
		return err
	}
	return nil
}

// validateSlots enforces the shared schema limits.
func validateSlots(slots []loadoutSlot) error {
	if len(slots) > MaxSlots {
		return fmt.Errorf("too many slots: %d (max %d)", len(slots), MaxSlots)
	}
	for i, slot := range slots {
		if len(slot.Items) < 1 || len(slot.Items) > 2 {
			return fmt.Errorf("slot %d: items must have 1 or 2 entries", i+1)
		}
		if slot.Items[0].Gem == "" {
			return fmt.Errorf("slot %d: item gem is empty", i+1)
		}
		if len(slot.Items) == 2 && slot.Items[1].Hash == "" {
			return fmt.Errorf("slot %d: second item hash is empty", i+1)
		}
		for _, item := range slot.Items {
			if item.Level < 0 || item.Level > 200 {
				return fmt.Errorf("slot %d: level out of range", i+1)
			}
		}
	}
	return nil
}

// SaveLoadout writes the player configuration (same schema as the mod reads:
// {lang, slots:[{items:[...],enabled}]}; a bare array is accepted too for
// backwards compatibility). Atomic write (temp + rename) so the mod's 250ms
// mtime tick never sees a half-written file.
func (s *LoadoutService) SaveLoadout(config string) error {
	var c struct {
		Lang  string        `json:"lang"`
		Slots []loadoutSlot `json:"slots"`
	}
	if err := json.Unmarshal([]byte(config), &c); err != nil {
		return err
	}
	slots := c.Slots
	if slots == nil {
		// legacy bare-array config
		var arr []loadoutSlot
		if err := json.Unmarshal([]byte(config), &arr); err != nil {
			return err
		}
		slots = arr
	}
	if err := validateSlots(slots); err != nil {
		return err
	}
	dir := userCfgDir()
	if err := os.MkdirAll(dir, 0755); err != nil {
		return err
	}
	path := filepath.Join(dir, "loadout.json")
	tmp := path + ".tmp"
	if err := os.WriteFile(tmp, []byte(config), 0644); err != nil {
		return err
	}
	return os.Rename(tmp, path)
}
