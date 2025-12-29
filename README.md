# Radio Premium

Aplicación de radio online premium para Windows con WinUI 3.

## Características

- 🎵 **Radio Browser API**: Busca y reproduce miles de emisoras de radio online
- 🔍 **Identificación de canciones**: Captura el audio del sistema y reconoce canciones con ACRCloud
- 💚 **Integración con Spotify**: Añade canciones identificadas a tu playlist "Radio Likes"
- ⭐ **Favoritos**: Guarda tus emisoras favoritas para acceso rápido
- 🎨 **UI Premium**: Interfaz moderna con Fluent Design

## Requisitos

- Windows 10 versión 1809 o superior
- .NET 8.0 SDK
- Visual Studio 2022 con carga de trabajo "Desarrollo de aplicaciones de escritorio .NET"
- Windows App SDK 1.5+

## Configuración

### ACRCloud

Las credenciales de ACRCloud ya están configuradas en `appsettings.json`.

### Spotify

1. Crea una aplicación en [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Añade `radiopremium://callback` como Redirect URI
3. Copia el Client ID a `appsettings.json`

## Estructura del proyecto

```
RadioPremium/
├── src/
│   ├── RadioPremium.App/          # Proyecto WinUI 3 (UI)
│   │   ├── Views/                 # Páginas XAML
│   │   ├── Controls/              # Controles personalizados
│   │   ├── Converters/            # Value converters
│   │   └── Helpers/               # Utilidades
│   │
│   ├── RadioPremium.Core/         # Lógica de negocio
│   │   ├── Models/                # Modelos de datos
│   │   ├── ViewModels/            # ViewModels (MVVM)
│   │   ├── Services/              # Interfaces de servicios
│   │   └── Messages/              # Mensajes del messenger
│   │
│   └── RadioPremium.Infrastructure/  # Implementaciones
│       └── Services/              # Implementación de servicios
│
└── RadioPremium.sln
```

## Compilar y ejecutar

```powershell
cd RadioPremium
dotnet restore
dotnet build
dotnet run --project src/RadioPremium.App
```

O abre `RadioPremium.sln` en Visual Studio y presiona F5.

## Atajos de teclado

| Atajo | Acción |
|-------|--------|
| `Espacio` | Reproducir / Pausar |
| `Ctrl+I` | Identificar canción |
| `Ctrl+Shift+S` | Añadir a Spotify |
| `Ctrl+,` | Abrir ajustes |

## APIs utilizadas

- **Radio Browser API**: https://api.radio-browser.info
- **ACRCloud**: https://www.acrcloud.com
- **Spotify Web API**: https://developer.spotify.com/documentation/web-api

## Licencia

MIT License
