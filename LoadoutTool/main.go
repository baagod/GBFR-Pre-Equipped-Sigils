package main

import (
	"embed"
	"log"
	"os"

	"github.com/wailsapp/wails/v3/pkg/application"
)

//go:embed all:frontend/dist
var assets embed.FS

var app *application.App
var win *application.WebviewWindow

func main() {
	// "--minimized" (used by the mod pre-warm-up) starts hidden so the tool
	// is instantly available via the in-game hotkey without flashing a window.
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
			// X button = minimise instead of closing (window stays alive so
			// the in-game hotkey can bring it back instantly).
			WndProcInterceptor: func(hwnd uintptr, msg uint32, wParam, lParam uintptr) (uintptr, bool) {
				if msg == 0x0010 { // WM_CLOSE
					if win != nil {
						win.Minimise()
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
		Title:            "GBFR 预配装配置",
		Width:            900,
		Height:           700,
		URL:              "/",
		Hidden:           hidden,
		BackgroundColour: application.NewRGB(10, 10, 10),
	})

	if err := app.Run(); err != nil {
		log.Fatal(err)
	}
}
