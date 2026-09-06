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
	procSetWindowLong                = user32.NewProc("SetWindowLongW")
	procSetLayeredWindowAttributes  = user32.NewProc("SetLayeredWindowAttributes")
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

// trayIcon returns the embedded game trait icon (tray + window).
func trayIcon() []byte {
	return trayIconBytes
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
		Name:     "Loadout",
		Icon:     trayIconBytes,
		Services: []application.Service{
			application.NewService(&LoadoutService{}),
		},
		Assets: application.AssetOptions{
			Handler: application.AssetFileServerFS(assets),
		},
		Windows: application.WindowsOptions{
			DisableQuitOnLastWindowClosed: true,
			// Soft compositing avoids the white GPU frame flash when a hidden
			// WebView2 window is woken back up.
			AdditionalBrowserArgs: []string{},
			// X button = hide to tray; WM_APP+0x10 = internal show request.
			// A WebviewWindow HWND accessor does not exist in beta.16, so we
			// filter by message instead: both messages are window-specific.
			WndProcInterceptor: handleWndMsg,
		},
		Mac: application.MacOptions{
			ApplicationShouldTerminateAfterLastWindowClosed: true,
		},
	})

	win = app.Window.NewWithOptions(application.WebviewWindowOptions{
		Title:               "GBFR Pre-Equipped Sigils",
		Width:               760,
		Height:              840,
		MinWidth:            760, // fully locked at 760x840
		MaxWidth:            760,
		MinHeight:           840,
		MaxHeight:           840,
		MaximiseButtonState: application.ButtonDisabled,
		URL:                 "/",
		Hidden:              hidden,
		BackgroundColour:    application.NewRGB(10, 10, 10),
	})
	// Force the WebView2 backing colour to the theme background so restoring
	// a hidden window does not flash a white frame before content renders.
	win.SetBackgroundColour(application.NewRGB(10, 10, 10))

	// System tray: single click toggles the window; menu offers quit.
	tray := app.SystemTray.New()
	tray.SetIcon(trayIcon())
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
// internal show/restore nudges). Wails exposes no HWND accessor in beta.16,
// so each message doubles as a window-specific command.
func handleWndMsg(_ uintptr, msg uint32, _, _ uintptr) (uintptr, bool) {
	if win == nil {
		return 0, false
	}
	nudge := func() {
		win.SetSize(759, 799)
		win.SetSize(760, 800)
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
