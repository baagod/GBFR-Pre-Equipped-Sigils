package main

import (
	"embed"
	"log"
	"os"
	"syscall"
	"time"
	"unsafe"

	"github.com/wailsapp/wails/v3/pkg/application"
)

//go:embed all:frontend/dist
var assets embed.FS

//go:embed icons/tray.png
var trayIconBytes []byte

var app *application.App
var win *application.WebviewWindow

const mutexName = "Local\\GBFRPreEquippedSigilsTool"

var (
	user32                         = syscall.NewLazyDLL("user32.dll")
	procFindWindowW                = user32.NewProc("FindWindowW")
	procIsWindowVisible            = user32.NewProc("IsWindowVisible")
	procGetForegroundWindow        = user32.NewProc("GetForegroundWindow")
	procPostMessageW               = user32.NewProc("PostMessageW")
	procSetForegroundWindow        = user32.NewProc("SetForegroundWindowW")
	procShowWindow                 = user32.NewProc("ShowWindow")
	procGetWindowLong              = user32.NewProc("GetWindowLongW")
	procSetWindowLong              = user32.NewProc("SetWindowLongW")
	procSetLayeredWindowAttributes = user32.NewProc("SetLayeredWindowAttributes")
	kernel32                       = syscall.NewLazyDLL("kernel32.dll")
	procCreateMutexW               = kernel32.NewProc("CreateMutexW")
	procGetLastError               = kernel32.NewProc("GetLastError")
	procReleaseMutex               = kernel32.NewProc("ReleaseMutex")
)

// ensureSingleInstance: second launches activate the existing window and exit.
func ensureSingleInstance() (release func()) {
	namePtr := uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(mutexName)))
	handle, _, cerr := procCreateMutexW.Call(0, 0, namePtr)
	if handle == 0 {
		log.Printf("single-instance: mutex create failed (handle=0), continuing without lock")
		return func() {}
	}
	if cerr == syscall.ERROR_ALREADY_EXISTS {
		log.Printf("single-instance: existing instance detected, activating its window")
		title, _ := syscall.UTF16PtrFromString("GBFR Pre-Equipped Sigils")
		hwnd, _, _ := procFindWindowW.Call(0, uintptr(unsafe.Pointer(title)))
		if hwnd != 0 {
			procShowWindow.Call(hwnd, 5) // SW_SHOW
			procPostMessageW.Call(hwnd, 0x8010, 0, 0)
			procSetForegroundWindow.Call(hwnd)
		}
		os.Exit(0)
	}
	return func() {
		procReleaseMutex.Call(handle)
	}
}

func main() {
	releaseMutex := ensureSingleInstance()
	defer releaseMutex()

	app = application.New(application.Options{
		Name: "Loadout",
		Icon: trayIconBytes,
		Services: []application.Service{
			application.NewService(&LoadoutService{}),
		},
		Assets: application.AssetOptions{
			Handler: application.AssetFileServerFS(assets),
		},
		Windows: application.WindowsOptions{
			DisableQuitOnLastWindowClosed: true,
			// X button = hide to tray; WM_APP+0x10 = internal show request.
			// A WebviewWindow HWND accessor is not exposed by this Wails
			// version, so each message doubles as a window-specific command.
			WndProcInterceptor: handleWndMsg,
		},
	})

	win = app.Window.NewWithOptions(application.WebviewWindowOptions{
		Title: "GBFR Pre-Equipped Sigils",
		// Wails v3 sizes are the full window frame (incl. title bar) in DIP.
		// Fixed size: DisableResize removes the resize border, so the user
		// cannot resize; the maximise button is disabled too.
		Width:               760,
		Height:              840,
		DisableResize:       true,
		MaximiseButtonState: application.ButtonDisabled,
		URL:                 "/",
		Hidden:              false,
		BackgroundColour:    application.NewRGB(10, 10, 10),
	})
	// Force the WebView2 backing colour to the theme background so restoring
	// a hidden window does not flash a white frame before content renders.
	win.SetBackgroundColour(application.NewRGB(10, 10, 10))

	// System tray: single click toggles the window; menu offers quit.
	tray := app.SystemTray.New()
	tray.SetIcon(trayIconBytes)
	tray.SetTooltip("GBFR Pre-Equipped Sigils")
	tray.AttachWindow(win)

	tray.OnClick(func() { go trayOnClick() })
	menu := application.NewMenu()
	menu.Add("Exit").OnClick(func(*application.Context) { app.Quit() })
	tray.SetMenu(menu)
	tray.Show()

	if err := app.Run(); err != nil {
		log.Fatal(err)
	}
}

// handleWndMsg filters the window messages we care about (hide-on-close and
// internal show/restore nudges). 0x8010 is the shared activation message
// (the mod's hotkey also posts it) and doubles as the show command.
func handleWndMsg(_ uintptr, msg uint32, _, _ uintptr) (uintptr, bool) {
	if win == nil {
		return 0, false
	}
	// Repaint nudge: a 1px size round-trip forces the hidden WebView2 frame to
	// redraw after activation. With no Min/Max set, SetSize has no hidden
	// min/max side effects, so the round-trip ends at the design size.
	nudge := func() {
		win.SetSize(759, 799)
		win.SetSize(760, 840)
	}
	switch msg {
	case 0x0010: // WM_CLOSE
		win.Hide()
		return 0, true
	case 0x8010: // activate: restore + show + repaint nudge + focus (single activation command)
		win.Restore()
		win.Show()
		nudge()
		win.Focus()
		return 0, true
	}
	return 0, false
}

// trayOnClick shows/restores the window according to its state.
func trayOnClick() {
	defer func() {
		if r := recover(); r != nil {
			log.Printf("tray click panic: %v", r)
		}
	}()
	title, _ := syscall.UTF16PtrFromString("GBFR Pre-Equipped Sigils")
	hwnd, _, _ := procFindWindowW.Call(0, uintptr(unsafe.Pointer(title)))
	if hwnd == 0 {
		return
	}
	// One activation command for every state: 0x8010 restores+shows the window
	// (the tool then nudges a repaint and focuses). Hidden windows get the
	// layered fade-in first so the WebView2 frame does not flash white; a
	// minimized window still reports visible and goes through the plain path.
	if visible, _, _ := procIsWindowVisible.Call(hwnd); visible != 0 {
		if fg, _, _ := procGetForegroundWindow.Call(); fg != hwnd {
			procPostMessageW.Call(hwnd, 0x8010, 0, 0)
		}
	} else {
		exStyle, _, _ := procGetWindowLong.Call(hwnd, uintptr(^uintptr(0)-19)) // GWL_EXSTYLE=-20
		procSetWindowLong.Call(hwnd, uintptr(^uintptr(0)-19), exStyle|0x80000) // WS_EX_LAYERED
		procSetLayeredWindowAttributes.Call(hwnd, 0, 0, 0x2)
		procPostMessageW.Call(hwnd, 0x8010, 0, 0)
		go func() {
			time.Sleep(150 * time.Millisecond)
			procSetLayeredWindowAttributes.Call(hwnd, 0, 255, 0x2)
		}()
	}
	procSetForegroundWindow.Call(hwnd)
}
