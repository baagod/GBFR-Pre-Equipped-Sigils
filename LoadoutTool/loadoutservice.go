package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
)

// MaxSlots mirrors the managed/native effective limit (must stay in sync).
const MaxSlots = 22

// LoadoutService reads/writes the mod directory data files next to the exe.
// Protocol is shared with the mod: traits.json (dictionary) and loadout.json
// (player configuration, same shape as the pre-loadout.json fallback).
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

type loadoutSlot struct {
	Trait1  string `json:"trait1"`
	Level1  int    `json:"level1"`
	Trait2  string `json:"trait2"`
	Level2  int    `json:"level2"`
	Enabled bool   `json:"enabled"`
}

func exeDir() string {
	exe, err := os.Executable()
	if err != nil {
		return "."
	}
	return filepath.Dir(exe)
}

func (s *LoadoutService) LoadTraits() (string, error) {
	data, err := os.ReadFile(filepath.Join(exeDir(), "traits.json"))
	if err != nil {
		return "", err
	}
	return string(data), nil
}

// LoadConfig returns loadout.json, or pre-loadout.json as the editable
// starting point when no player configuration exists yet.
func (s *LoadoutService) LoadConfig() (string, error) {
	dir := exeDir()
	data, err := os.ReadFile(filepath.Join(dir, "loadout.json"))
	if err != nil {
		data, err = os.ReadFile(filepath.Join(dir, "pre-loadout.json"))
		if err != nil {
			return "", err
		}
	}
	return string(data), nil
}

// SaveLoadout writes the player configuration (same schema as the mod reads).
// Atomic write (temp + rename) so the mod's 250ms mtime tick never sees a
// half-written file.
func (s *LoadoutService) SaveLoadout(config string) error {
	var parsed map[string][]loadoutSlot
	if err := json.Unmarshal([]byte(config), &parsed); err != nil {
		return err
	}
	slots := parsed["slots"]
	if len(slots) > MaxSlots {
		return fmt.Errorf("too many slots: %d (max %d)", len(slots), MaxSlots)
	}
	for i, slot := range slots {
		if slot.Level1 < 0 || slot.Level1 > 200 || slot.Level2 < 0 || slot.Level2 > 200 {
			return fmt.Errorf("slot %d: level out of range", i+1)
		}
	}
	path := filepath.Join(exeDir(), "loadout.json")
	tmp := path + ".tmp"
	if err := os.WriteFile(tmp, []byte(config), 0644); err != nil {
		return err
	}
	return os.Rename(tmp, path)
}
