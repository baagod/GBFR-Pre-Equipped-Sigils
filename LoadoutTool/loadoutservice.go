package main

import (
	"encoding/json"
	"os"
	"path/filepath"
)

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
// The mod persists it in HotkeyConfig.json (Reloaded-II configurable, enum
// "MenuHotkey"). Missing/unparseable config falls back to F1 (0x70).
func (s *LoadoutService) GetHotkey() (int, error) {
	data, err := os.ReadFile(filepath.Join(exeDir(), "HotkeyConfig.json"))
	if err != nil {
		return 0x70, nil
	}
	var cfg struct {
		MenuHotkey json.RawMessage `json:"MenuHotkey"`
	}
	if err := json.Unmarshal(data, &cfg); err != nil || len(cfg.MenuHotkey) == 0 {
		return 0x70, nil
	}
	var numeric int
	if err := json.Unmarshal(cfg.MenuHotkey, &numeric); err == nil {
		return numeric, nil
	}
	var name string
	if err := json.Unmarshal(cfg.MenuHotkey, &name); err == nil {
		keyCodes := map[string]int{
			"F1": 0x70, "F2": 0x71, "F3": 0x72, "F4": 0x73,
			"F5": 0x74, "F6": 0x75, "F7": 0x76, "F8": 0x77,
			"F9": 0x78, "F10": 0x79, "F11": 0x7A, "F12": 0x7B,
			"Insert": 0x2D, "Delete": 0x2E, "Home": 0x24, "End": 0x23,
		}
		if vk, ok := keyCodes[name]; ok {
			return vk, nil
		}
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
func (s *LoadoutService) SaveLoadout(config string) error {
	var parsed map[string][]loadoutSlot
	if err := json.Unmarshal([]byte(config), &parsed); err != nil {
		return err
	}
	return os.WriteFile(
		filepath.Join(exeDir(), "loadout.json"),
		[]byte(config),
		0644)
}
