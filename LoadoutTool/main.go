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
	procPostMessageW               = user32.NewProc("PostMessageW")
	procSetForegroundWindow        = user32.NewProc("SetForegroundWindowW")
	procShowWindow                 = user32.NewProc("ShowWindow")
	procIsIconic                   = user32.NewProc("IsIconic")
	procGetWindowLong                = user32.NewProc("GetWindowLongW")
	procSetWindowLong                = user32.NewProc("SetWindowLongW")
	procSetLayeredWindowAttributes  = user32.NewProc("SetLayeredWindowAttributes")
	procSetWindowPos                = user32.NewProc("SetWindowPos")
	procKeybdEvent                  = user32.NewProc("keybd_event")
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
			procShowWindow.Call(hwnd, 9) // SW_RESTORE`n`t`t`t`t`tprocSetWindowLong.Call(hwnd, -20, (procGetWindowLong.Call(hwnd, -20))[0]|0x80000)`n`t`t`t`t`tprocSetLayeredWindowAttributes.Call(hwnd, 0, 0, 0x2)`n`t`t`t`t`tprocPostMessageW.Call(hwnd, 0x8010, 0, 0)`n`t`t`t`t`tgo func() {`n`t`t`t`t`t`ttime.Sleep(150 * time.Millisecond)`n`t`t`t`t`t`tprocSetLayeredWindowAttributes.Call(hwnd, 0, 255, 0x2)`n`t`t`t`t`t}()`n`t`t`t`t`tprocKeybdEvent.Call(0x12, 0, 0, 0) // VK_MENU down (grants foreground right)
			procKeybdEvent.Call(0x12, 0, 2, 0) // VK_MENU up
			procSetWindowPos.Call(hwnd, 0xFFFFFFFF, 0, 0, 0, 0, 0x0001|0x0002|0x0040)
			procSetForegroundWindow.Call(hwnd)
			procSetWindowPos.Call(hwnd, 0xFFFFFFFE, 0, 0, 0, 0, 0x0001|0x0002) // internal show (repaint-safe)
			procKeybdEvent.Call(0x12, 0, 0, 0) // VK_MENU down (grants foreground right)
			procKeybdEvent.Call(0x12, 0, 2, 0) // VK_MENU up
			procSetWindowPos.Call(hwnd, 0xFFFFFFFF, 0, 0, 0, 0, 0x0001|0x0002|0x0040)
			procSetForegroundWindow.Call(hwnd)
			procSetWindowPos.Call(hwnd, 0xFFFFFFFE, 0, 0, 0, 0, 0x0001|0x0002)
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
		Name:     "loadouttool",
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
			WndProcInterceptor: func(hwnd uintptr, msg uint32, wParam, lParam uintptr) (uintptr, bool) {
				if win == nil {
					return 0, false
				}
				if msg == 0x0010 { // WM_CLOSE
					win.Hide()
					return 0, true
				}
				if msg == 0x8010 { // WM_APP+0x10: internal show + focus + repaint nudge
					win.Show()
					win.SetSize(759, 799)
					win.SetSize(760, 800)
					win.Focus()
					return 0, true
				}
				if msg == 0x8011 { // WM_APP+0x11: internal restore + focus + repaint nudge
					win.Restore()
					win.Show()
					win.SetSize(759, 799)
					win.SetSize(760, 800)
					win.Focus()
					return 0, true
				}
				if msg == 0x8012 { // WM_APP+0x12: hide->show bounce (repaints WebView after minimize)
					win.Hide()
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
		Title:               "GBFR Pre-Equipped Sigils",
		Width:               760,
		Height:              800,
		MinWidth:            760, // fully locked at 760x800
		MaxWidth:            760,
		MinHeight:           800,
		MaxHeight:           800,
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

	tray.OnClick(func() {
		go func() {
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
			// Minimized windows repaint badly after external restore, so bounce
			// through hide->show; hidden windows just get the internal show.
			if iconic, _, _ := procIsIconic.Call(hwnd); iconic != 0 {
				procPostMessageW.Call(hwnd, 0x8011, 0, 0)
			} else {
				// Layered fade-in: hide the white frame under alpha 0, show,
				// then fade to opaque once content has rendered.
				exStyle, _, _ := procGetWindowLong.Call(hwnd, uintptr(^uintptr(0)-19)) // GWL_EXSTYLE=-20
				procSetWindowLong.Call(hwnd, uintptr(^uintptr(0)-19), exStyle|0x80000) // WS_EX_LAYERED
				procSetLayeredWindowAttributes.Call(hwnd, 0, 0, 0x2)
				procPostMessageW.Call(hwnd, 0x8010, 0, 0)
				go func() {
					time.Sleep(150 * time.Millisecond)
					procSetLayeredWindowAttributes.Call(hwnd, 0, 255, 0x2)
				}()
			}
			procKeybdEvent.Call(0x12, 0, 0, 0) // VK_MENU down (grants foreground right)
			procKeybdEvent.Call(0x12, 0, 2, 0) // VK_MENU up
			procSetWindowPos.Call(hwnd, 0xFFFFFFFF, 0, 0, 0, 0, 0x0001|0x0002|0x0040)
			procSetForegroundWindow.Call(hwnd)
			procSetWindowPos.Call(hwnd, 0xFFFFFFFE, 0, 0, 0, 0, 0x0001|0x0002)
		}()
	})
	menu := application.NewMenu()
	menu.Add("Exit").OnClick(func(*application.Context) { app.Quit() })
	tray.SetMenu(menu)
	tray.Show()

	if err := app.Run(); err != nil {
		log.Fatal(err)
	}
}
