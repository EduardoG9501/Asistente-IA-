# Trading Assistant IA

Plataforma integral de asistencia y análisis de trading que combina análisis técnico con inteligencia artificial (Google Gemini), monitoreo automatizado de mercados y notificaciones en tiempo real vía Telegram.

## 🚀 Arquitectura del Proyecto

- **TradingAssistant.API**: API REST construida con ASP.NET Core 8.0 y servidor de archivos estáticos para el Dashboard Web.
- **TradingAssistant.Core**: Modelos de dominio, entidades e interfaces base de la aplicación.
- **TradingAssistant.Infrastructure**: Implementación de servicios externos (API de TwelveData, cliente Gemini IA, bot de Telegram, persistencia con Entity Framework Core y SQLite).
- **TradingAssistant.Tests**: Pruebas unitarias y de integración.
- **TradingAssistantApp**: Aplicación móvil multiplataforma desarrollada en Flutter / Dart.
- **Web Dashboard**: Interfaz web moderna (HTML, Tailwind CSS, JavaScript) alojada en `TradingAssistant.API/wwwroot`.

---

## ⚙️ Configuración y Requisitos

### Requisitos previos
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Flutter SDK](https://flutter.dev/docs/get-started/install) (opcional, para la app móvil)

### 1. Clonar el repositorio
```bash
git clone <URL_DEL_REPOSITORIO>
cd "Asistente IA"
```

### 2. Configurar credenciales y variables
En `TradingAssistant.API/`, crea una copia de `appsettings.Example.json` llamada `appsettings.json`:

```bash
cp TradingAssistant.API/appsettings.Example.json TradingAssistant.API/appsettings.json
```

Edita `appsettings.json` con tus credenciales:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=trading_assistant.db"
  },
  "TradingSettings": {
    "TwelveDataApiKey": "TU_TWELVE_DATA_API_KEY",
    "GeminiApiKey": "TU_GEMINI_API_KEY",
    "TelegramBotToken": "TU_TELEGRAM_BOT_TOKEN",
    "DefaultTelegramChatId": "TU_TELEGRAM_CHAT_ID",
    "Symbol": "EUR/USD",
    "Timeframe": "15m",
    "CheckIntervalSeconds": 60
  }
}
```

---

## 💻 Ejecución

### Ejecutar Backend y Dashboard Web
```bash
cd TradingAssistant.API
dotnet run
```
Accede desde tu navegador:
- **Dashboard Web**: `http://localhost:5000` (o el puerto asignado)
- **Documentación Swagger**: `http://localhost:5000/swagger`

### Ejecutar Tests
```bash
dotnet test
```

### Ejecutar Aplicación Flutter
```bash
cd TradingAssistantApp
flutter pub get
flutter run
```

---

## 🛡️ Seguridad
Las claves de API privadas, tokens de Telegram y archivos de base de datos locales (`*.db`) están excluidos en `.gitignore` para proteger la información confidencial.
