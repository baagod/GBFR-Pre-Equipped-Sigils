package main

import (
	"embed"
	"log"

	"github.com/wailsapp/wails/v3/pkg/application"
)

//go:embed all:frontend/dist
var assets embed.FS

func main() {
	app := application.New(application.Options{
		Name: "loadouttool",
		Services: []application.Service{
			application.NewService(&LoadoutService{}),
		},
		Assets: application.AssetOptions{
			Handler: application.AssetFileServerFS(assets),
		},
		Mac: application.MacOptions{
			ApplicationShouldTerminateAfterLastWindowClosed: true,
		},
	})

	app.Window.NewWithOptions(application.WebviewWindowOptions{
		Title:            "GBFR 预配装配置",
		Width:            900,
		Height:           700,
		URL:              "/",
		BackgroundColour: application.NewRGB(10, 10, 10),
	})

	if err := app.Run(); err != nil {
		log.Fatal(err)
	}
}
