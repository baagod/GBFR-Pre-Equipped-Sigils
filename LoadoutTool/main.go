package main

import (
	"bytes"
	"embed"
	"image"
	"image/color"
	"image/png"
	"log"
	"os"
	"syscall"
	"unsafe"

	"github.com/wailsapp/wails/v3/pkg/application"
)

//go:embed all:frontend/dist
var assets embed.FS

var app *application.App
var win *application.WebviewWindow

const mutexName = "Local\\GBFRPreEquippedSigilsTool"

var (
	user32                         = syscall.NewLazyDLL("user32.dll")
	procFindWindowW                = user32.NewProc("FindWindowW")
	procPostMessageW               = user32.NewProc("PostMessageW")
	procSetForegroundWindow        = user32.NewProc("SetForegroundWindowW")
	procShowWindow                 = user32.NewProc("ShowWindow")
	kernel32                       = syscall.NewLazyDLL("kernel32.dll")
	procCreateMutexW               = kernel32.NewProc("CreateMutexW")
	procGetLastError               = kernel32.NewProc("GetLastError")
	procReleaseMutex               = kernel32.NewProc("ReleaseMutex")
)

// ensureSingleInstance: second launches activate the existing window and exit.
func ensureSingleInstance() (release func()) {
	handle, _, _ := procCreateMutexW.Call(0, 0, uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(mutexName))))
	if handle == 0 {
		return func() {}
	}
	if err, _, _ := procGetLastError.Call(); err == 183 { // ERROR_ALREADY_EXISTS
		title, _ := syscall.UTF16PtrFromString("GBFR Pre-Equipped Sigils")
		hwnd, _, _ := procFindWindowW.Call(0, uintptr(unsafe.Pointer(title)))
		if hwnd != 0 {
			procShowWindow.Call(hwnd, 5)            // SW_SHOW
			procPostMessageW.Call(hwnd, 0x8010, 0, 0) // internal show (repaint-safe)
			procSetForegroundWindow.Call(hwnd)
		}
		os.Exit(0)
	}
	return func() {
		procReleaseMutex.Call(handle)
	}
}

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
	releaseMutex := ensureSingleInstance()
	defer releaseMutex()

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
			// X button = hide to tray; WM_APP+0x10 = internal show request.
			// A WebviewWindow HWND accessor does not exist in beta.16, so we
			// filter by message instead: both messages are window-specific.
			WndProcInterceptor: func(hwnd uintptr, msg uint32, wParam, lParam uintptr) (uintptr, bool) {
				if win == nil {
					return 0, false
				}
				if msg == 0x0010 { // WM_CLOSE
					win.Hide()
					return 0, true
				}
				if msg == 0x8010 { // WM_APP+0x10: internal show request (repaint-safe)
					win.Show()
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
