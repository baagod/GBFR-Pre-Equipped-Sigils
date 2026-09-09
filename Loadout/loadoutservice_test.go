package main

import (
	"os"
	"path/filepath"
	"testing"
)

func slot(gem, sec string, lvl, secLvl int) loadoutSlot {
	items := []loadoutItem{{Gem: gem, Level: lvl}}
	if sec != "" {
		items = append(items, loadoutItem{Hash: sec, Level: secLvl})
	}
	return loadoutSlot{Items: items, Enabled: true}
}

func TestUserCfgDirMatchesModPath(t *testing.T) {
	base := filepath.Join("C:", "Users", "someone", "AppData", "Local")
	t.Setenv("LOCALAPPDATA", base)
	want := filepath.Join(base, "GBFRPreEquippedSigils")
	if got := userCfgDir(); got != want {
		t.Errorf("userCfgDir() = %q, want %q", got, want)
	}
}

func TestValidateSlots(t *testing.T) {
	many := make([]loadoutSlot, MaxSlots+1)
	for i := range many {
		many[i] = slot("9A60FBF0", "", 15, 0)
	}
	cases := []struct {
		name string
		cfg  []loadoutSlot
		ok   bool
	}{
		{"valid single", []loadoutSlot{slot("9A60FBF0", "", 15, 0)}, true},
		{"valid pair", []loadoutSlot{slot("9A60FBF0", "B5FF9FD3", 15, 15)}, true},
		{"three items", []loadoutSlot{{
			Items: []loadoutItem{
				{Gem: "9A60FBF0", Level: 15},
				{Hash: "B5FF9FD3", Level: 15},
				{Hash: "E69A4694", Level: 15},
			},
		}}, false},
		{"empty second hash", []loadoutSlot{{
			Items: []loadoutItem{{Gem: "9A60FBF0", Level: 15}, {Hash: "", Level: 15}},
		}}, false},
		{"negative level", []loadoutSlot{slot("9A60FBF0", "B5FF9FD3", -1, 15)}, false},
		{"too many slots", many, false},
		{"exactly 12 slots", func() []loadoutSlot {
			out := make([]loadoutSlot, MaxSlots)
			for i := range out {
				out[i] = slot("9A60FBF0", "", 15, 0)
			}
			return out
		}(), true},
		{"13 rows one disabled", func() []loadoutSlot {
			out := make([]loadoutSlot, MaxSlots+1)
			for i := range out {
				out[i] = slot("9A60FBF0", "", 15, 0)
			}
			out[0].Enabled = false
			return out
		}(), true},
		{"empty items", []loadoutSlot{{}}, false},
		{"missing gem", []loadoutSlot{slot("", "", 15, 0)}, false},
		{"bad main level", []loadoutSlot{slot("9A60FBF0", "B5FF9FD3", 201, 15)}, false},
		{"bad sec level", []loadoutSlot{slot("9A60FBF0", "B5FF9FD3", 15, 201)}, false},
	}
	for _, c := range cases {
		if err := validateSlots(c.cfg); (err == nil) != c.ok {
			t.Errorf("%s: got err=%v want ok=%v", c.name, err, c.ok)
		}
	}
}

func TestSaveLoadoutWritesAndLeavesNoTempFiles(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("LOCALAPPDATA", dir)
	cfg := `{"lang":"zh","slots":[{"items":[{"gem":"9A60FBF0","level":15}],"enabled":true}]}`
	if err := (&LoadoutService{}).SaveLoadout(cfg); err != nil {
		t.Fatalf("SaveLoadout: %v", err)
	}
	path := filepath.Join(dir, "GBFRPreEquippedSigils", "loadout.json")
	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("read back: %v", err)
	}
	if string(data) != cfg {
		t.Errorf("stored config = %q, want %q", data, cfg)
	}
	// The unique temp file must not linger next to the config.
	entries, err := os.ReadDir(filepath.Dir(path))
	if err != nil {
		t.Fatalf("read dir: %v", err)
	}
	if len(entries) != 1 || entries[0].Name() != "loadout.json" {
		t.Errorf("unexpected files beside loadout.json: %v", entries)
	}
}

func TestSaveLoadoutOverwritesExisting(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("LOCALAPPDATA", dir)
	svc := &LoadoutService{}
	first := `{"lang":"zh","slots":[{"items":[{"gem":"9A60FBF0","level":15}],"enabled":true}]}`
	second := `{"lang":"en","slots":[{"items":[{"gem":"B5FF9FD3","level":10}],"enabled":false}]}`
	if err := svc.SaveLoadout(first); err != nil {
		t.Fatalf("first save: %v", err)
	}
	if err := svc.SaveLoadout(second); err != nil {
		t.Fatalf("second save: %v", err)
	}
	data, err := os.ReadFile(filepath.Join(dir, "GBFRPreEquippedSigils", "loadout.json"))
	if err != nil {
		t.Fatalf("read back: %v", err)
	}
	if string(data) != second {
		t.Errorf("stored config = %q, want %q", data, second)
	}
}

func TestSaveLoadoutRejectsInvalidWithoutTouchingDisk(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("LOCALAPPDATA", dir)
	// Structural violations are rejected before any directory or file is
	// created, so a rejected save must leave no trace on disk.
	if err := (&LoadoutService{}).SaveLoadout(`{"slots":[{"items":[],"enabled":true}]}`); err == nil {
		t.Fatal("expected a validation error")
	}
	path := filepath.Join(dir, "GBFRPreEquippedSigils", "loadout.json")
	if _, err := os.Stat(path); !os.IsNotExist(err) {
		t.Fatalf("target file must not exist after a rejected save (stat err=%v)", err)
	}
}
