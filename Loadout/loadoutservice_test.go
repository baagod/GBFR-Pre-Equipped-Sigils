package main

import "testing"

func slot(gem, sec string, lvl, secLvl int) loadoutSlot {
	items := []loadoutItem{{Gem: gem, Level: lvl}}
	if sec != "" {
		items = append(items, loadoutItem{Hash: sec, Level: secLvl})
	}
	return loadoutSlot{Items: items, Enabled: true}
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
		{"too many slots", many, false},
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
