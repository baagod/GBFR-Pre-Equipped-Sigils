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

// MinimiseApp hides the window to the taskbar; the process stays alive so the
// in-game hotkey can bring the window back instantly. Invoked by Esc (and the
// X button via the WM_CLOSE interceptor).
func (s *LoadoutService) MinimiseApp() {
	if win != nil {
		win.Minimise()
	}
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
