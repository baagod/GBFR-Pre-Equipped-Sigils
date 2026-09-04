package main

import (
	"bytes"
	"embed"
	"image"
	"image/color"
	"image/png"
	"log"
	"os"

	"github.com/wailsapp/wails/v3/pkg/application"
)

//go:embed all:frontend/dist
var assets embed.FS

var app *application.App
var win *application.WebviewWindow

// trayIcon returns a simple 16x16 icon (grey rounded square) for the tray.
func trayIcon() []byte {
	img := image.NewRGBA(image.Rect(0, 0, 16, 16))
	for y := 0; y < 16; y++ {
		for x := 0; x < 16; x++ {
			dx, dy := float64(x)-7.5, float64(y)-7.5
			if dx*dx+dy*dy <= 52 {
				img.Set(x, y, color.RGBA{0x9E, 0x9E, 0x9E, 0xFF})
			}
		}
	}
	var buf bytes.Buffer
	_ = png.Encode(&buf, img)
	return buf.Bytes()
}

func main() {
	// "--minimized" (used by old pre-warm) starts hidden; kept for compat.
	hidden := false
	for _, arg := range os.Args {
		if arg == "--minimized" {
			hidden = true
		}
	}

	app = application.New(application.Options{
		Name: "loadouttool",
		Services: []application.Service{
			application.NewService(&LoadoutService{}),
		},
		Assets: application.AssetOptions{
			Handler: application.AssetFileServerFS(assets),
		},
		Windows: application.WindowsOptions{
			DisableQuitOnLastWindowClosed: true,
			// X button = hide to tray (window stays alive; the in-game hotkey
			// restores it via the WM_APP+0x10 show request).
			WndProcInterceptor: func(hwnd uintptr, msg uint32, wParam, lParam uintptr) (uintptr, bool) {
				if msg == 0x0010 { // WM_CLOSE
					if win != nil {
						win.Hide()
					}
					return 0, true
				}
				if msg == 0x8010 { // WM_APP+0x10: internal show request (repaint-safe)
					if win != nil {
						win.Show()
					}
					return 0, true
				}
				return 0, false
			},
		},
		Mac: application.MacOptions{
			ApplicationShouldTerminateAfterLastWindowClosed: true,
		},
	})

	win = app.Window.NewWithOptions(application.WebviewWindowOptions{
		Title:            "GBFR Pre-Equipped Sigils",
		Width:            900,
		Height:           700,
		URL:              "/",
		Hidden:           hidden,
		BackgroundColour: application.NewRGB(10, 10, 10),
	})

	// System tray: single click toggles the window; menu offers quit.
	tray := app.SystemTray.New()
	tray.SetIcon(trayIcon())
	tray.SetTooltip("GBFR 配装工具")
	tray.AttachWindow(win)
	menu := application.NewMenu()
	menu.Add("退出").OnClick(func(*application.Context) { app.Quit() })
	tray.SetMenu(menu)
	tray.Show()

	if err := app.Run(); err != nil {
		log.Fatal(err)
	}
}
